using System;
using System.Collections.Generic;

namespace NobleMuffins.LimbHacker.Guts
{
	public class CollectionPool<TCollection, TElement> where TCollection: class, ICollection<TElement>
	{
		private readonly object key = new object ();

		private readonly int poolSize;
		private readonly TCollection[] poolTable;
		private readonly bool[] useTable;
		private readonly Func<int, TCollection> instantiateWithCapacity;
        private readonly Func<TCollection, int> getCapacity;

        public CollectionPool (int poolSize, Func<TCollection, int> getCapacity, Func<int, TCollection> instantiateWithCapacity)
		{
			this.poolSize = poolSize;
            this.getCapacity = getCapacity;
            this.instantiateWithCapacity = instantiateWithCapacity;
            poolTable = new TCollection[poolSize];
            useTable = new bool[poolSize];
        }

		public DisposableBundle<TCollection> Get (int desiredCapacity)
		{
			TCollection o = null;
			lock (key) {
				for(int i = 0; i < poolSize; i++) {
					if(!useTable[i] && !object.ReferenceEquals(poolTable[i], null) && getCapacity(poolTable[i]) >= desiredCapacity) {
						o = poolTable[i];
						o.Clear();
						useTable[i] = true;
						break;
					}
				}
			}
			if(o == null) {
				o = instantiateWithCapacity(desiredCapacity);
			}
			return new DisposableBundle<TCollection>(o, Release);
		}

		public IDisposable Get(int desiredCapacity, out TCollection collection) {
			var bundle = Get(desiredCapacity);
			collection = bundle.Object;
			return bundle;
		}

		private void Release (TCollection o)
		{
			lock(key) {
				var foundPlace = false;

				//First try to find its place, if it's already in the pool table.
				for(int i = 0; i < poolSize; i++) {
					if(object.ReferenceEquals(poolTable[i], o)) {
						useTable[i] = false;
						foundPlace = true;
						break;
					}
				}

				//If that failed, than try to find an empty slot.
				if(foundPlace == false) {
					for(int i = 0; i < poolSize; i++) {
						if(object.ReferenceEquals(poolTable[i], null)) {
							poolTable[i] = o;
							useTable[i] = false;
							foundPlace = true;
							break;
						}
					}
				}

				//If that failed, than try to find a smaller collection that isn't in use and replace it.
				if(foundPlace == false) {
					for(int i = 0; i < poolSize; i++) {
						if(!useTable[i] && !object.ReferenceEquals(poolTable[i], null) && poolTable[i].Count < o.Count) {
							poolTable[i] = o;
							break;
						}
					}
				}
			}
		}
	}
}
