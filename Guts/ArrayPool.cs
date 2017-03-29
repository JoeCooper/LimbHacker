using System;

namespace NobleMuffins.LimbHacker.Guts
{
	public class ArrayPool<TElement>
	{
		private readonly object key = new object ();

		private readonly int poolSize;
		private readonly TElement[][] poolTable;
		private readonly bool[] useTable;

		public ArrayPool (int poolSize)
		{
			this.poolSize = poolSize;
			poolTable = new TElement[poolSize][];
			useTable = new bool[poolSize];
		}

		public DisposableBundle<TElement[]> Get (int desiredCapacity, bool clear)
		{
			TElement[] array = null;
			lock (key) {
				for(int i = 0; i < poolSize; i++) {
					if(!useTable[i] && !object.ReferenceEquals(poolTable[i], null) && poolTable[i].Length >= desiredCapacity) {
						array = poolTable[i];
						useTable[i] = true;
						break;
					}
				}
			}
			if(array == null) {
				var capacity = RoundUpToNearestSquare(desiredCapacity);
				array = new TElement[capacity];
			}
			else if(clear) {
				for(int i = 0; i < array.Length; i++) {
					array[i] = default(TElement);
				}
			}
			return new DisposableBundle<TElement[]>(array, Release);
		}

		public IDisposable Get(int desiredCapacity, bool clear, out TElement[] collection) {
			var bundle = Get(desiredCapacity, clear);
			collection = bundle.Object;
			return bundle;
		}

		private void Release (TElement[] array)
		{
			lock(key) {
				var foundPlace = false;

				//First try to find its place, if it's already in the pool table.
				for(int i = 0; i < poolSize; i++) {
					if(object.ReferenceEquals(poolTable[i], array)) {
						useTable[i] = false;
						foundPlace = true;
						break;
					}
				}

				//If that failed, than try to find an empty slot.
				if(foundPlace == false) {
					for(int i = 0; i < poolSize; i++) {
						if(object.ReferenceEquals(poolTable[i], null)) {
							poolTable[i] = array;
							useTable[i] = false;
							foundPlace = true;
							break;
						}
					}
				}

				//If that failed, than try to find a smaller collection that isn't in use and replace it.
				if(foundPlace == false) {
					for(int i = 0; i < poolSize; i++) {
						if(!useTable[i] && !object.ReferenceEquals(poolTable[i], null) && poolTable[i].Length < array.Length) {
							poolTable[i] = array;
							break;
						}
					}
				}
			}
		}

		private int RoundUpToNearestSquare(int minimum) {
			int newCapacity = 1;
			do {
				newCapacity *= 2;
			}
			while(newCapacity < minimum);
			return newCapacity;
		}
	}
}
