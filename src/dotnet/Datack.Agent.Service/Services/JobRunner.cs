﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datack.Agent.Models;
using Datack.Agent.Services.Tasks;
using Datack.Common.Models.Data;
using Microsoft.Extensions.Logging;

namespace Datack.Agent.Services
{
    public class JobRunner
    {
        public static readonly ConcurrentDictionary<Guid, CancellationTokenSource> RunningTasks = new();

        private static readonly SemaphoreSlim ExecuteJobRunLock = new(1, 1);

        private readonly ILogger<JobRunner> _logger;
        private readonly RpcService _rpcService;

        private readonly Dictionary<String, BaseTask> _tasks;

        public JobRunner(ILogger<JobRunner> logger,
                         RpcService rpcService,
                         CreateBackupTask createBackupTask,
                         CompressTask compressTask,
                         DeleteFileTask deleteTask,
                         DeleteS3Task deleteS3Task,
                         UploadAzureTask uploadAzureTask,
                         UploadS3Task uploadS3Task)
        {
            _logger = logger;
            _rpcService = rpcService;

            _tasks = new Dictionary<String, BaseTask>
            {
                {
                    "createBackup", createBackupTask
                },
                {
                    "compress", compressTask
                },
                {
                    "deleteFile", deleteTask
                },
                {
                    "deleteS3", deleteS3Task
                },
                {
                    "uploadAzure", uploadAzureTask
                },
                {
                    "uploadS3", uploadS3Task
                }
            };

            foreach (var (_, task) in _tasks)
            {
                task.OnCompleteEvent += async (_, evt) =>
                {
                    if (evt.IsError)
                    {
                        _logger.LogError("{jobRunTaskId}: {message}", evt.JobRunTaskId, evt.Message);
                    }
                    else
                    {
                        _logger.LogInformation("{jobRunTaskId}: {message}", evt.JobRunTaskId, evt.Message);
                    }

                    RunningTasks.TryRemove(evt.JobRunTaskId, out var _);

                    await _rpcService.QueueComplete(evt);
                };
                task.OnProgressEvent += async (_, evt) =>
                {
                    if (evt.IsError)
                    {
                        _logger.LogError("{jobRunTaskId}: {message}", evt.JobRunTaskId, evt.Message);
                    }
                    else
                    {
                        _logger.LogInformation("{jobRunTaskId}: {message}", evt.JobRunTaskId, evt.Message);
                    }

                    await _rpcService.QueueProgress(evt);
                };
            }
        }

        public async Task ExecuteJobRunTask(JobRunTask jobRunTask, JobRunTask previousTask, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Running job run task {jobRunTaskId}", jobRunTask.JobRunTaskId);

            // Make sure only 1 process executes a job run otherwise it might run duplicate tasks.
            var receivedLockSuccesfully = await ExecuteJobRunLock.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

            try
            {
                if (!receivedLockSuccesfully)
                {
                    // Lock timed out
                    _logger.LogError("Could not obtain executeJobRunLock within 30 seconds for job run task {jobRunTaskId}", jobRunTask.JobRunTaskId);

                    return;
                }

                _logger.LogDebug("Entering lock for job run {jobRunTaskId}", jobRunTask.JobRunTaskId);

                try
                {
                    if (jobRunTask.Type == null)
                    {
                        throw new ArgumentException("Task type cannot be null");
                    }

                    if (!_tasks.TryGetValue(jobRunTask.Type, out var task))
                    {
                        throw new Exception($"Unknown task type {jobRunTask.Type}");
                    }
                    
                    if (RunningTasks.TryGetValue(jobRunTask.JobRunTaskId, out var runningTask))
                    {
                        _logger.LogDebug("Task {jobRunTaskId} is already running ", jobRunTask.JobRunTaskId);
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        CancellationTokenSource cancellationTokenSource;
                        if (jobRunTask.JobTask.Timeout > 0)
                        {
                            cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(jobRunTask.JobTask.Timeout.Value));
                        }
                        else
                        {
                            cancellationTokenSource = new CancellationTokenSource();
                        }

                        if (!RunningTasks.TryAdd(jobRunTask.JobRunTaskId, cancellationTokenSource))
                        {
                            _logger.LogDebug("Task {jobRunTaskId} cannot be added", jobRunTask.JobRunTaskId);
                            return;
                        }

                        await task.Run(jobRunTask, previousTask, cancellationTokenSource.Token);
                    }, cancellationToken);
                }
                finally
                {
                    _logger.LogDebug("Releasing lock for job run {jobRunTaskId}", jobRunTask.JobRunTaskId);
                    ExecuteJobRunLock.Release();
                }
            }
            catch (Exception ex)
            {
                await _rpcService.QueueComplete(new CompleteEvent
                {
                    IsError = true,
                    JobRunTaskId = jobRunTask.JobRunTaskId,
                    Message = ex.Message,
                    ResultArtifact = null
                });
            }
        }

        public void StopTask(Guid jobRunTaskId)
        {
            _logger.LogDebug($"Stopping job run task {jobRunTaskId}");

            RunningTasks.TryGetValue(jobRunTaskId, out var cancellationTokenSource);

            if (cancellationTokenSource == null)
            {
                _logger.LogDebug($"Cancellation token for job task {jobRunTaskId} not found");

                return;
            }

            cancellationTokenSource.Cancel();
        }

        public void StopAllTasks()
        {
            foreach (var runningTask in RunningTasks)
            {
                runningTask.Value.Cancel();
            }
        }
    }
}
