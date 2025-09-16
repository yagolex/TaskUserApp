namespace TaskUser.Api.Services
{
    public interface ITaskReassigner
    {
        Task ReassignAsync(CancellationToken ct);
    }
}