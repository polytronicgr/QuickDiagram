﻿using System.Linq;
using Codartis.SoftVis.Diagramming.Graph.Layout.Incremental;
using Codartis.SoftVis.Diagramming.Graph.Layout.Incremental.Relative.Logic;
using Codartis.SoftVis.Diagramming.UnitTests.Diagramming.Layout.Incremental.Builders;
using Codartis.SoftVis.Diagramming.UnitTests.Diagramming.Layout.Incremental.TestSubjects;
using FluentAssertions;
using Xunit;

namespace Codartis.SoftVis.Diagramming.UnitTests.Diagramming.Layout.Incremental.Relative
{
    public class QuasiProperLayoutGraphTests
    {
        private readonly QuasiProperLayoutGraphBuilder _testGraphBuilder;
        private QuasiProperLayoutGraph TestGraph => _testGraphBuilder.Graph;

        public QuasiProperLayoutGraphTests()
        {
            _testGraphBuilder = new QuasiProperLayoutGraphBuilder();
        }

        [Fact]
        public void GetPrimaryParents_ChoosesFirstInNameOrder()
        {
            _testGraphBuilder.SetUp(
                "P2<-C",
                "P1<-C"
                );

            TestGraph.GetPrimaryParent(GetVertex("C")).ShouldBeEquivalentTo(GetVertex("P1"));
        }

        [Fact]
        public void GetPrimaryParents_ChoosesCloserNode()
        {
            _testGraphBuilder.SetUp(
                "P1<-*1<-C",
                "P1<-P2<-C"
                );

            TestGraph.GetPrimaryParent(GetVertex("C")).ShouldBeEquivalentTo(GetVertex("P2"));
        }

        [Fact]
        public void GetPrimaryParents_ChoosesHigherPriority()
        {
            ((TestLayoutVertex)_testGraphBuilder.AddVertex("P2")).SetPriority(2);
            _testGraphBuilder.SetUp(
                "P1<-C",
                "P2<-C"
                );

            TestGraph.GetPrimaryParent(GetVertex("C")).ShouldBeEquivalentTo(GetVertex("P2"));
        }

        [Fact]
        public void GetPrimaryChildren_Works()
        {
            ((TestLayoutVertex)_testGraphBuilder.AddVertex("P1")).SetPriority(2);
            _testGraphBuilder.SetUp(
                "P1<-C1",
                "P1<-C2",
                "P2<-C2",
                "P2<-C3"
                );

            TestGraph.GetPrimaryChildren(GetVertex("P1")).Select(i => i.Name)
                .ShouldBeEquivalentTo(new[]
                {
                    GetVertex("C1").Name,
                    GetVertex("C2").Name
                });
        }

        [Fact]
        public void GetPrimarySiblings_Works()
        {
            ((TestLayoutVertex)_testGraphBuilder.AddVertex("P1")).SetPriority(2);
            _testGraphBuilder.SetUp(
                "P1<-C1",
                "P1<-C2",
                "P2<-C2",
                "P2<-C3"
                );

            TestGraph.GetPrimarySiblings(GetVertex("C2")).Select(i => i.Name)
                .ShouldBeEquivalentTo(new[]
                {
                    GetVertex("C1").Name,
                });
        }

        [Fact]
        public void GetRank_Works()
        {
            _testGraphBuilder.SetUp(
                "P1<-C1<-C2"
                );

            TestGraph.GetRank(GetVertex("P1")).ShouldBeEquivalentTo(0);
            TestGraph.GetRank(GetVertex("C1")).ShouldBeEquivalentTo(1);
            TestGraph.GetRank(GetVertex("C2")).ShouldBeEquivalentTo(2);
        }

        [Fact]
        public void IsProper_WhenTrue()
        {
            _testGraphBuilder.SetUp(
                "P1<-C1<-C2", 
                "P1<-C3<-C2"
                );

            TestGraph.IsProper().Should().BeTrue();
        }

        [Fact]
        public void IsProper_WhenFalse()
        {
            _testGraphBuilder.SetUp(
                "P1<-C1<-C2", 
                "P1<-C2"
                );

            TestGraph.IsProper().Should().BeFalse();
        }

        private LayoutVertexBase GetVertex(string name)
        {
            return _testGraphBuilder.GetVertex(name);
        }
    }
}
