namespace Aexxa.CanvasCore
{
    public interface IPoolable
    {
        /// <summary>Called once, right after the instance is first created by the pool.</summary>
        void OnCreated();

        /// <summary>Called every time the instance is taken from the pool to be shown.</summary>
        void OnSpawn(object context);

        /// <summary>Called every time the instance is returned to the pool. Reset all state here.</summary>
        void OnDespawn();
    }
}
