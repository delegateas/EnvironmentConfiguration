namespace ESB.DeveloperExperience;

public interface IEsbWorker
{
    Task ProcessSession(CancellationToken cancellationToken);
}
