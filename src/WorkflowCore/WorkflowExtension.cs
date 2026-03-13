using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace WorkflowCore;

public static class WorkflowExtension
{
    public static async Task StartOrResumeWorkflowInSingleMode(this IWorkflowHost workflowHost, string workflowName,
        object data = null)
    {
        //检查workflow是否存在
        var workflows = (await workflowHost.PersistenceStore.FindWorkflowByDefinitionId(workflowName))
            .Where(n => (int)n.Status <= 1).ToList();
        var count = workflows.Count;
        switch (count)
        {
            case > 1:
                throw new Exception("当前有多个实例,无法进行自动控制");
            case 0:
                await workflowHost.StartWorkflow(workflowName, data);
                return;
            default:
                await workflowHost.ResumeWorkflow(workflows[0].Id);
                break;
        }
    }

    public static async Task SuspendWorkflowInSingleMode(this IWorkflowHost workflowHost, string workflowName)
    {
        var workflows = (await workflowHost.PersistenceStore.FindWorkflowByDefinitionId(workflowName))
            .Where(n => n.Status == WorkflowStatus.Runnable).ToList();
        var count = workflows.Count;

        switch (count)
        {
            case > 1:
                throw new Exception("当前有多个实例,无法进行暂停");
            case 0:
                return;
            default:
                await workflowHost.SuspendWorkflow(workflows[0].Id);
                break;
        }
    }

    public static async Task TerminateWorkflowInSingleMode(this IWorkflowHost workflowHost, string workflowName)
    {
        var workflows = (await workflowHost.PersistenceStore.FindWorkflowByDefinitionId(workflowName)).Where(n => (int)n.Status <= 1).ToList();
        var count = workflows.Count;
        if (count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            await workflowHost.TerminateWorkflow(workflows[i].Id);
        }
    }

    public static Task<List<WorkflowInstance>> FindWorkflowByDefinitionId(this IWorkflowHost workflowHost, string workflowName)
    {
        return workflowHost.PersistenceStore.FindWorkflowByDefinitionId(workflowName);
    }

    public static async Task<bool> ExistsRunnableWorkflowByName(this IWorkflowHost workflowHost, string workflowName)
    {
        var workflows = await workflowHost.PersistenceStore.FindWorkflowByDefinitionId(workflowName);
        return workflows.Any(n => n.Status == WorkflowStatus.Runnable);
    }
}