using Aexxa.CanvasCore;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Tests.EditMode
{
    public class RecycledScrollViewTests
    {
        private sealed class TestCell : MonoBehaviour, IRecycledScrollCell
        {
            public int? BoundIndex;
            public void Bind(int index) => BoundIndex = index;
        }

        private GameObject _root;
        private GameObject _cellPrefabSource;
        private RecycledScrollView _scrollView;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ScrollRoot", typeof(RectTransform), typeof(ScrollRect));
            var rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.zero;
            rootRt.sizeDelta = new Vector2(200, 400); // acts as the viewport: rect.height = 400

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_root.transform, false);
            var content = (RectTransform)contentGo.transform;

            _cellPrefabSource = new GameObject("CellPrefab", typeof(RectTransform), typeof(TestCell));

            _scrollView = _root.AddComponent<RecycledScrollView>();
            var so = new SerializedObject(_scrollView);
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("cellPrefab").objectReferenceValue = _cellPrefabSource.GetComponent<TestCell>();
            so.FindProperty("cellSize").floatValue = 50f;
            so.FindProperty("overscanCells").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_cellPrefabSource);
        }

        [Test]
        public void SetItemCount_PoolSizeStaysBounded_RegardlessOfItemCount()
        {
            // viewport 400 / cellSize 50 = 8 visible + 1 = 9, + overscan 2*2 = 13
            _scrollView.SetItemCount(10_000);

            Assert.AreEqual(13, _root.transform.Find("Content").childCount,
                "Pool should be sized to the viewport, not to the item count.");
        }

        [Test]
        public void SetItemCount_SmallerThanPool_OnlyCreatesAsManyCellsAsItems()
        {
            _scrollView.SetItemCount(5);

            Assert.AreEqual(5, _root.transform.Find("Content").childCount);
        }

        [Test]
        public void SetItemCount_BindsVisibleIndicesWithoutDuplicates_StartingAtZero()
        {
            _scrollView.SetItemCount(10_000);

            var content = _root.transform.Find("Content");
            var seenIndices = new System.Collections.Generic.HashSet<int>();

            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                var cell = child.GetComponent<TestCell>();
                Assert.IsTrue(cell.BoundIndex.HasValue, "Every active cell must have been Bind()-ed.");
                Assert.IsTrue(seenIndices.Add(cell.BoundIndex.Value), "No two active cells should be bound to the same index.");
            }

            Assert.IsTrue(seenIndices.Contains(0), "Scrolled to the top, index 0 should be among the visible cells.");
        }
    }
}
