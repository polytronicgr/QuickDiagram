﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Codartis.SoftVis.VisualStudioIntegration.Modeling;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Document = Microsoft.CodeAnalysis.Document;
using Task = System.Threading.Tasks.Task;
using TextSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace Codartis.SoftVis.VisualStudioIntegration.Hosting
{
    /// <summary>
    /// Gets information from Visual Studio about the current solution, projects, source documents.
    /// </summary>
    internal class HostWorkspaceGateway : IRoslynModelProvider, IVsRunningDocTableEvents, IDisposable
    {
        private const string CSharpContentTypeName = "CSharp";

        private readonly IPackageServices _packageServices;
        private uint _runningDocumentTableCookie;
        private IVsRunningDocumentTable _runningDocumentTable;
        private IWpfTextView _activeWpfTextView;

        internal HostWorkspaceGateway(IPackageServices packageServices)
        {
            _packageServices = packageServices;
        }

        public async Task InitAsync()
        {
            _runningDocumentTable = await _packageServices.GetRunningDocumentTableServiceAsync();
            await InitializeRunningDocumentTableAsync();
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            Debug.WriteLine("OnBeforeDocumentWindowShow started.");
            var wpfTextView = VsWindowFrameToWpfTextView(pFrame);
            Debug.WriteLine($"wpfTextView={wpfTextView}");
            if (wpfTextView != null)
            {
                var contentType = wpfTextView.TextBuffer.ContentType;
                Debug.WriteLine($"contentType={contentType}");

                _activeWpfTextView = contentType.IsOfType(CSharpContentTypeName)
                    ? wpfTextView
                    : null;
            }

            Debug.WriteLine("OnBeforeDocumentWindowShow finished.");
            return VSConstants.S_OK;
        }

        public async Task<Workspace> GetWorkspaceAsync()
        {
            return await _packageServices.GetVisualStudioWorkspaceAsync();
        }

        public async Task<ISymbol> GetCurrentSymbolAsync()
        {
            var document = GetCurrentDocument();
            Debug.WriteLine($"document={document}");
            if (document == null)
                return null;

            var syntaxTree = await document.GetSyntaxTreeAsync();
            var syntaxRoot = await syntaxTree.GetRootAsync();
            var span = GetSelection();
            var currentNode = syntaxRoot.FindNode(span);

            var semanticModel = await document.GetSemanticModelAsync();
            var symbol = GetSymbolForSyntaxNode(semanticModel, currentNode);
            Debug.WriteLine($"symbol={symbol}");
            return symbol;
        }

        private static ISymbol GetSymbolForSyntaxNode(SemanticModel semanticModel, SyntaxNode node)
        {
            if (node is TypeDeclarationSyntax ||
                node is EnumDeclarationSyntax ||
                node is DelegateDeclarationSyntax)
                return semanticModel.GetDeclaredSymbol(node);

            var identifierNode = FindSimpleNameSyntax(node);
            return identifierNode == null
                ? null
                : semanticModel.GetSymbolInfo(identifierNode).Symbol;
        }

        private static SimpleNameSyntax FindSimpleNameSyntax(SyntaxNode node)
        {
            var simpleNameSyntax = node as SimpleNameSyntax;
            if (simpleNameSyntax != null)
                return simpleNameSyntax;

            foreach (var childNode in node.ChildNodes())
            {
                simpleNameSyntax = FindSimpleNameSyntax(childNode);
                if (simpleNameSyntax != null)
                    break;
            }

            return simpleNameSyntax;
        }

        public async Task<bool> HasSourceAsync(ISymbol symbol)
        {
            return await GetDocumentIdAsync(symbol) != null;
        }

        public async Task ShowSourceAsync(ISymbol symbol)
        {
            var location = symbol?.Locations.FirstOrDefault();
            if (location == null)
                return;

            var documentId = await GetDocumentIdAsync(symbol);
            if (documentId == null)
                return;

            var workspace = await GetWorkspaceAsync();
            workspace.OpenDocument(documentId, activate: true);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            SelectSourceLocation(location.GetLineSpan().Span);
        }

        private void SelectSourceLocation(LinePositionSpan span)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var hostService = _packageServices.GetHostEnvironmentService();
            var selection = hostService.ActiveDocument.Selection as TextSelection;
            if (selection == null)
                return;

            selection.MoveTo(span.Start.Line + 1, span.Start.Character + 1, Extend: false);
            selection.MoveTo(span.End.Line + 1, span.End.Character + 1, Extend: true);
        }

        private Document GetCurrentDocument()
        {
            if (_activeWpfTextView == null)
                return null;

            var currentSnapshot = _activeWpfTextView.TextBuffer.CurrentSnapshot;
            var contentType = currentSnapshot.ContentType;
            if (!contentType.IsOfType(CSharpContentTypeName))
                return null;

            var document = currentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
            return document;
        }

        private async Task<DocumentId> GetDocumentIdAsync(ISymbol symbol)
        {
            var workspace = await GetWorkspaceAsync();

            var location = symbol?.Locations.FirstOrDefault();
            if (location == null)
                return null;

            return workspace?.CurrentSolution?.GetDocumentId(location.SourceTree);
        }

        private TextSpan GetSelection()
        {
            var visualStudioSpan = _activeWpfTextView.Selection.StreamSelectionSpan.SnapshotSpan.Span;
            var roslynSpan = new TextSpan(visualStudioSpan.Start, visualStudioSpan.Length);
            return roslynSpan;
        }

        private static IWpfTextView VsWindowFrameToWpfTextView(IVsWindowFrame vsWindowFrame)
        {
            IWpfTextView wpfTextView = null;
            var textView = VsShellUtilities.GetTextView(vsWindowFrame);
            if (textView != null)
            {
                var riidKey = DefGuidList.guidIWpfTextViewHost;
                object pvtData;
                var vsUserData = (IVsUserData)textView;
                if (vsUserData.GetData(ref riidKey, out pvtData) == 0 && pvtData != null)
                    wpfTextView = ((IWpfTextViewHost)pvtData).TextView;
            }
            return wpfTextView;
        }

        private async Task InitializeRunningDocumentTableAsync()
        {
            Debug.WriteLine("InitializeRunningDocumentTableAsync started.");
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _runningDocumentTable?.AdviseRunningDocTableEvents(this, out _runningDocumentTableCookie);
            Debug.WriteLine("InitializeRunningDocumentTableAsync finished.");
        }

        void IDisposable.Dispose()
        {
            if ((int)_runningDocumentTableCookie == 0)
                return;

            ThreadHelper.JoinableTaskFactory.Run(async delegate {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _runningDocumentTable.UnadviseRunningDocTableEvents(_runningDocumentTableCookie);
                _runningDocumentTableCookie = 0U;
            });
        }
    }
}
