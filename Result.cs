
namespace HTW.Result{
	public record Result<T>{
		public record Success(T t):Result<T>;
		public record Failure(string Error):Result<T>;		
	}
}
