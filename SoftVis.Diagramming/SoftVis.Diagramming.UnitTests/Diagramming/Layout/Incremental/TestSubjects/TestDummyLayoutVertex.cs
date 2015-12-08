﻿using Codartis.SoftVis.Diagramming.Graph.Layout.Incremental;

namespace Codartis.SoftVis.Diagramming.UnitTests.Diagramming.Layout.Incremental.TestSubjects
{
    internal class TestDummyLayoutVertex : DummyLayoutVertex
    {
        public TestDummyLayoutVertex(int id)
            : base(id)
        {
        }

        public override string Name => $"*{Id}";
        public override string ToString() => Name;
    }
}
