
using System.Diagnostics;

namespace HTW.Result
{
	public record Result<T>
	{
		public readonly bool error;
		private readonly T? value;
		private readonly Exception? eval;

		private Result(T value)
		{
			this.error = false;
			this.value = value;
		}
		private Result(Exception err)
		{
			this.error = true;
			this.eval = err;
		}

		public static Result<T> Some(T value) => new(value);
		public static Result<T> None(Exception error) => new(error);
		public T? UnpackValue() => value!;
		public Exception? UnpackException() => eval!;
		
		public Result<U> Bind<U>(Func<T, Result<U>> func) => !error ? func(value!) :
		    Result<U>.None(eval!);
	}
}
