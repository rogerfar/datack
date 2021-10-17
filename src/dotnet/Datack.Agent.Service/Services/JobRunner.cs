﻿using System;
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
        private readonly ILogger<JobRunner> _logger;
        private readonly RpcService _rpcService;

        private readonly SemaphoreSlim _executeJobRunLock = new SemaphoreSlim(1, 1);

        private readonly Dictionary<String, BaseTask> _tasks;

        public JobRunner(ILogger<JobRunner> logger,
                         RpcService rpcService,
                         CreateBackupTask createBackupTask,
                         CompressTask compressTask,
                         UploadS3Task uploadS3Task)
        {
            _logger = logger;
            _rpcService = rpcService;

            _tasks = new Dictionary<String, BaseTask>
            {
                {
                    "create_backup", createBackupTask
                },
                {
                    "compress", compressTask
                },
                {
                    "upload_s3", uploadS3Task
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

                    await _rpcService.SendComplete(evt);
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

                    await _rpcService.SendProgress(evt);
                };
            }
        }

        public async Task ExecuteJobRunTask(Server server, JobRunTask jobRunTask, JobRunTask previousTask, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Running job run task {jobRunTaskId}", jobRunTask.JobRunTaskId);

            // Make sure only 1 process executes a job run otherwise it might run duplicate tasks.
            var receivedLockSuccesfully = await _executeJobRunLock.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

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

                    _ = Task.Run(() => task.Run(server, jobRunTask, previousTask, cancellationToken), cancellationToken);
                }
                finally
                {
                    _logger.LogDebug("Releasing lock for job run {jobRunTaskId}", jobRunTask.JobRunTaskId);
                    _executeJobRunLock.Release();
                }
            }
            catch (Exception ex)
            {
                await _rpcService.SendComplete(new CompleteEvent
                {
                    IsError = true,
                    JobRunTaskId = jobRunTask.JobRunTaskId,
                    Message = ex.Message,
                    ResultArtifact = null
                });
            }
        }
    }
}