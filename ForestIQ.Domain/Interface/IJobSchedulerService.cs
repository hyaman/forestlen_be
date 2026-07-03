namespace ForestIQ.Domain.Interface
{
    public interface IJobSchedulerService
    {
        void AddOrUpdateJob(string jobName, string cronExpression);
        void RemoveJob(string jobName);
        void SyncAllJobs();
    }
}
