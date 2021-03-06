﻿using Codartis.SoftVis.Diagramming;

namespace Codartis.SoftVis.UI
{
    /// <summary>
    /// Abstraction for the UI of a diagram connector.
    /// </summary>
    public interface IDiagramConnectorUi : IDiagramShapeUi
    {
        IDiagramConnector DiagramConnector { get; }
    }
}