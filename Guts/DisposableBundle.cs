using System;

namespace NobleMuffins.LimbHacker.Guts
{
	public class DisposableBundle<TObject>: IDisposable {
		public DisposableBundle(TObject datum, Action<TObject> callback) {
			this.datum = datum;
			this.callback = callback;
			disposed = false;
		}

		private readonly TObject datum;
		private readonly Action<TObject> callback;
		private bool disposed;

		public TObject Object { get {
				return datum;
			} }

		public void Dispose() {
			if(!disposed) {
				disposed = true;
				callback(Object);
			}
		}
	}
}

