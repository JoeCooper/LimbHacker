using System;
using System.Collections.Generic;

namespace NobleMuffins.LimbHacker.Guts
{
	public abstract class AbstractDisposableProduct: IDisposable
	{
		IEnumerable<IDisposable> dependencies;

		public AbstractDisposableProduct(IEnumerable<IDisposable> dependecies)
		{
			this.dependencies = dependecies;
		}
		
		public void Dispose()
		{
			foreach(var dependency in dependencies)
			{
				dependency.Dispose();
			}
		}
	}
}
