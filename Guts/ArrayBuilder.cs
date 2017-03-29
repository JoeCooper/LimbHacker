using System.Collections.Generic;

namespace NobleMuffins.LimbHacker.Guts {
	
	//This is an unsafe white-box class that is part of the Turbo Slicer black-box. The
	//differences between it and the .NET List are esoteric, specific and not relevant
	//to your needs.
	
	//Do not, under any circumstances, see it as a faster List for general use.
	//Read on only if you are studying or modifying TurboSlice.
	
	/* Shea's Law states, "The ability to improve a design occurs primarily at the interfaces.
	 *  This is also the prime location for screwing it up."
	 * 
	 * This class provides nice examples of both.
	 * 
	 * List.AddRange was eating up a large chunk of time according to the profiler. This method only
	 * accepts IEnumerable. While this is good in its use case, it doesn't have access to the given
	 * set's size and discovering its size creates a lot of unnecessary work. Therefore, the first
	 * special feature of TurboList is that its interface lets it observe a given set's size.
	 * 
	 * The second is more dangerous; its model is directly exposed. Another chunk of time spent was getting
	 * at the data, copying it and sometimes simply getting an array from the List.
	 * 
	 * Do not use this class for anything else and do not assume that this will make anything else faster.
	 * It was designed to meet a particular use case - the Muffin Slicer's - and is a private subset of that class
	 * for a reason.
	 */
	public class ArrayBuilder<T>: IList<T> {
		public T[] array;
		public int length = 0;
		
		public T[] ToArray()
		{
			T[] a = new T[length];
			System.Array.Copy(array, a, length);
			return a;
		}

		public ArrayBuilder(T[] copySource)
		{
			var capacity = RoundUpToNearestSquare(copySource.Length);
			array = new T[capacity];
			System.Array.Copy(copySource, array, copySource.Length);
			length = copySource.Length;
		}

		public ArrayBuilder(): this(0) {
		}
		
		public ArrayBuilder(int desiredCapacity)
		{
			var capacity = RoundUpToNearestSquare(desiredCapacity);
			array = new T[capacity];
			length = 0;
		}
		
		public void EnsureCapacity(int i)
		{			
			bool mustExpand = i > array.Length;
			
			if(mustExpand)
			{
				System.Array.Resize<T>(ref array, RoundUpToNearestSquare(i));
			}
		}
		
		public void AddArray(T[] source)
		{
			EnsureCapacity(source.Length + length);
			System.Array.Copy(source, 0, array, length, source.Length);
			length += source.Length;
		}

		public void AddArray(T[] source, int count)
		{
			count = System.Math.Min(count, source.Length);
			EnsureCapacity(count + length);
			System.Array.Copy(source, 0, array, length, count);
			length += count;
		}

		private int RoundUpToNearestSquare(int minimum) {
			int newCapacity = 1;
			do {
				newCapacity *= 2;
			}
			while(newCapacity < minimum);
			return newCapacity;
		}

        public int Capacity
        {
            get
            {
                return array.Length;
            }
        }

		public int Count {
			get {
				return length;
			}
		}

        public T this [int index]
        {
            get
            {
                return array[index];
            }
            set
            {
                array[index] = value;
            }
        }

        public void RemoveAt(int i)
        {
            throw new System.NotImplementedException();
        }

        public void Insert(int index, T figure)
        {
            throw new System.NotImplementedException();
        }

        public int IndexOf(T figure)
        {
            return System.Array.IndexOf(array, figure);
        }

		public void Add (T item)
		{
			EnsureCapacity(length + 1);
			array[length] = item;
			length++;
		}

		public bool Contains (T item)
		{
			throw new System.NotImplementedException ();
		}

		public void CopyTo (T[] array, int arrayIndex)
		{
			throw new System.NotImplementedException ();
		}

		public IEnumerator<T> GetEnumerator ()
		{
			throw new System.NotImplementedException ();
		}
		public void Clear() {
			length = 0;
		}

		public object SyncRoot {
			get {
				throw new System.NotImplementedException ();
			}
		}

		public bool Remove (T item)
		{
			throw new System.NotImplementedException ();
		}

		public bool IsReadOnly {
			get {
				return false;
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return array.GetEnumerator();
		}
	}
	
}