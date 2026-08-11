using Aexxa.CanvasCore;
using NUnit.Framework;
using UnityEngine;

namespace Aexxa.CanvasCore.Tests.EditMode
{
    public class UIElementPoolTests
    {
        private sealed class TestUIView : UIWidget
        {
            public static int CreatedCount;

            public override void OnCreated()
            {
                CreatedCount++;
            }
        }

        private GameObject _sourceGameObject;
        private UIView _sourcePrefab;
        private Transform _parent;

        [SetUp]
        public void SetUp()
        {
            TestUIView.CreatedCount = 0;
            _sourceGameObject = new GameObject("TestUIViewSource", typeof(CanvasGroup), typeof(TestUIView));
            _sourcePrefab = _sourceGameObject.GetComponent<UIView>();
            _parent = new GameObject("TestPoolParent").transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sourceGameObject);
            Object.DestroyImmediate(_parent.gameObject);
        }

        [Test]
        public void Prewarm_CreatesExactlyPrewarmCount()
        {
            _ = new UIElementPool(_sourcePrefab, _parent, prewarmCount: 2, maxSize: 5);

            Assert.AreEqual(2, TestUIView.CreatedCount);
        }

        [Test]
        public void Get_ReusesPrewarmedInstances_BeforeCreatingNew()
        {
            var pool = new UIElementPool(_sourcePrefab, _parent, prewarmCount: 2, maxSize: 5);

            var a = pool.Get();
            var b = pool.Get();

            Assert.AreEqual(2, TestUIView.CreatedCount,
                "Get() should reuse the 2 prewarmed instances instead of creating new ones.");

            pool.Release(a);
            pool.Release(b);
        }

        [Test]
        public void Release_ThenGet_ReturnsSameInstance_NoLeak()
        {
            var pool = new UIElementPool(_sourcePrefab, _parent, prewarmCount: 1, maxSize: 5);

            var first = pool.Get();
            pool.Release(first);
            var second = pool.Get();

            Assert.AreSame(first, second,
                "Releasing then getting again should reuse the same instance, not allocate a new one.");
            Assert.AreEqual(1, TestUIView.CreatedCount);

            pool.Release(second);
        }
    }
}
