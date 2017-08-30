﻿using System;
using System.Collections.Generic;
using System.Linq;
using Codartis.SoftVis.Diagramming;
using Codartis.SoftVis.Geometry;
using Codartis.SoftVis.Modeling;
using Codartis.SoftVis.UI;

namespace Codartis.SoftVis.Service
{
    /// <summary>
    /// Creates, aggregates and orchestrates model, diagram and UI services.
    /// </summary>
    public class VisualizationService : IVisualizationService
    {
        private const double DefaultMinZoom = .1;
        private const double DefaultMaxZoom = 10;
        private const double DefaultInitialZoom = 1;

        protected readonly IModelServiceFactory ModelServiceFactory;
        protected readonly IDiagramServiceFactory DiagramServiceFactory;
        protected readonly IUiServiceFactory UiServiceFactory;
        protected readonly IDiagramPluginFactory DiagramPluginFactory;
        protected readonly IEnumerable<DiagramPluginId> DiagramPluginIds;

        protected IModelService ModelService { get; }

        private readonly Dictionary<DiagramId, IDiagramService> _diagramServices;
        private readonly Dictionary<DiagramId, IUiService> _diagramUis;
        private readonly Dictionary<DiagramId, List<IDiagramPlugin>> _diagramPlugins;

        public event Action<IModelNode, DiagramId> ModelNodeInvoked;

        public VisualizationService(
            IModelServiceFactory modelServiceFactory,
            IDiagramServiceFactory diagramServiceFactory,
            IUiServiceFactory uiServiceFactory,
            IDiagramPluginFactory diagramPluginFactory,
            IEnumerable<DiagramPluginId> diagramPluginIds)
        {
            ModelServiceFactory = modelServiceFactory;
            DiagramServiceFactory = diagramServiceFactory;
            UiServiceFactory = uiServiceFactory;
            DiagramPluginFactory = diagramPluginFactory;
            DiagramPluginIds = diagramPluginIds;

            ModelService = ModelServiceFactory.Create();
            _diagramServices = new Dictionary<DiagramId, IDiagramService>();
            _diagramUis = new Dictionary<DiagramId, IUiService>();
            _diagramPlugins = new Dictionary<DiagramId, List<IDiagramPlugin>>();
        }

        public IModelService GetModelService() => ModelService;

        public DiagramId CreateDiagram(
            double minZoom = DefaultMinZoom,
            double maxZoom = DefaultMaxZoom,
            double initialZoom = DefaultInitialZoom)
        {
            var diagramId = DiagramId.Create();
            var diagramService = DiagramServiceFactory.Create();
            _diagramServices.Add(diagramId, diagramService);

            var diagramUi = CreateDiagramUi(diagramId, minZoom, maxZoom, initialZoom);
            _diagramUis.Add(diagramId, diagramUi);

            // Warning: plugins must be created after the UI so its event callbacks don't precede UI updates.
            var diagramPlugins = CreateAndAttachDiagramPlugins(DiagramPluginIds, ModelService, diagramService);
            _diagramPlugins.Add(diagramId, diagramPlugins.ToList());

            return diagramId;
        }

        public IDiagramService GetDiagramService(DiagramId diagramId) => _diagramServices[diagramId];
        public IUiService GetUiService(DiagramId diagramId) => _diagramUis[diagramId];

        private IUiService CreateDiagramUi(DiagramId diagramId,
            double minZoom = DefaultMinZoom,
            double maxZoom = DefaultMaxZoom,
            double initialZoom = DefaultInitialZoom)
        {
            var diagramStore = GetDiagramService(diagramId);
            var diagramUi = UiServiceFactory.Create(ModelService, diagramStore, minZoom, maxZoom, initialZoom);

            diagramUi.ShowModelItemsRequested += (modelNodes, followNewDiagramNodes) => OnShowModelItemsRequested(diagramId, modelNodes, followNewDiagramNodes);
            diagramUi.DiagramNodeSizeChanged += (diagramNode, size) => OnDiagramNodeSizeChanged(diagramId, diagramNode, size);
            diagramUi.DiagramNodeInvoked += node => OnDiagramNodeInvoked(diagramId, node);
            diagramUi.RemoveDiagramNodeRequested += diagramNode => OnRemoveDiagramNodeRequested(diagramId, diagramNode);

            return diagramUi;
        }

        private IEnumerable<IDiagramPlugin> CreateAndAttachDiagramPlugins(IEnumerable<DiagramPluginId> diagramPluginIds,
            IModelStore modelStore, IDiagramStore diagramStore)
        {
            foreach (var diagramPluginId in diagramPluginIds)
            {
                var diagramPlugin = DiagramPluginFactory.Create(diagramPluginId);
                diagramPlugin.Initialize(modelStore, diagramStore);
                yield return diagramPlugin;
            }
        }

        private void OnShowModelItemsRequested(DiagramId diagramId, IReadOnlyList<IModelNode> modelNodes, bool followNewDiagramNodes)
        {
            var diagramService = GetDiagramService(diagramId);

            foreach (var modelNode in modelNodes)
                diagramService.ShowModelNode(modelNode);

            if (followNewDiagramNodes)
            {
                var diagramStore = GetDiagramService(diagramId);
                var diagramNodes = modelNodes.Select(i => diagramStore.GetDiagramNodeById(i.Id)).ToArray();
                GetUiService(diagramId).FollowDiagramNodes(diagramNodes);
            }
        }

        private void OnDiagramNodeSizeChanged(DiagramId diagramId, IDiagramNode diagramNode, Size2D newSize)
        {
            GetDiagramService(diagramId).UpdateDiagramNodeSize(diagramNode, newSize);
        }

        private void OnDiagramNodeInvoked(DiagramId diagramId, IDiagramNode diagramNode)
        {
            ModelNodeInvoked?.Invoke(diagramNode.ModelNode, diagramId);
        }

        private void OnRemoveDiagramNodeRequested(DiagramId diagramId, IDiagramNode diagramNode)
        {
            GetDiagramService(diagramId).HideModelNode(diagramNode.ModelNode);
        }
    }
}