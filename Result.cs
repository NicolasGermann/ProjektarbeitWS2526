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

        public Result<U> TryBind<U>(Func<T, U> func)
        {
            if (error) return Result<U>.None(eval!);
            try
            {
                return Result<U>.Some(func(value!));
            }
            catch (Exception e)
            {
                return Result<U>.None(e);
            }
        }

        public Result<T> CatchBind(Action<Exception> func)
        {
            if (!error) return Result<T>.Some(value!);
            func(eval!);
            return Result<T>.None(eval!);
        }
    }
}
