﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datack.Common.Models.Data;
using Datack.Web.Service.Data;
using Microsoft.EntityFrameworkCore;

namespace Datack.Web.Service.Services
{
    public class JobTasks
    {
        private readonly DataContext _dataContext;
        private readonly RemoteService _remoteService;

        public JobTasks(DataContext dataContext, RemoteService remoteService)
        {
            _dataContext = dataContext;
            _remoteService = remoteService;
        }

        public async Task<IList<JobTask>> GetForJob(Guid jobId, CancellationToken cancellationToken)
        {
            return await _dataContext.JobTasks
                                     .AsNoTracking()
                                     .Where(m => m.JobId == jobId)
                                     .OrderBy(m => m.Order)
                                     .ToListAsync(cancellationToken);
        }

        public async Task<IList<JobTask>> GetForServer(Guid serverId, CancellationToken cancellationToken)
        {
            return await _dataContext.JobTasks
                                     .AsNoTracking()
                                     .Where(m => m.ServerId == serverId)
                                     .ToListAsync(cancellationToken);
        }

        public async Task<JobTask> GetById(Guid jobTaskId, CancellationToken cancellationToken)
        {
            return await _dataContext.JobTasks
                                     .AsNoTracking()
                                     .FirstOrDefaultAsync(m => m.JobTaskId == jobTaskId, cancellationToken);
        }

        public async Task<JobTask> Add(JobTask jobTask, CancellationToken cancellationToken)
        {
            jobTask.JobTaskId = Guid.NewGuid();

            var jobTaskCount = await _dataContext.JobTasks
                                                 .AsNoTracking()
                                                 .CountAsync(m => m.JobId == jobTask.JobId, cancellationToken);

            jobTask.Order = jobTaskCount;

            await _dataContext.JobTasks.AddAsync(jobTask, cancellationToken);
            await _dataContext.SaveChangesAsync(cancellationToken);

            try
            {
                var server = await _dataContext.Servers.FirstOrDefaultAsync(m => m.ServerId == jobTask.ServerId, cancellationToken);
                await _remoteService.UpdateJobTask(server, jobTask, cancellationToken);
            }
            catch
            {
                // ignored
            }

            return jobTask;
        }

        public async Task Update(JobTask jobTask, CancellationToken cancellationToken)
        {
            var dbJobTask = await _dataContext
                                  .JobTasks
                                  .FirstOrDefaultAsync(m => m.JobTaskId == jobTask.JobTaskId, cancellationToken);

            if (dbJobTask == null)
            {
                throw new Exception($"Job task with ID {jobTask.JobTaskId} not found");
            }

            dbJobTask.Name = jobTask.Name;
            dbJobTask.Description = jobTask.Description;
            dbJobTask.Order = jobTask.Order;
            dbJobTask.UsePreviousTaskArtifactsFromJobTaskId = jobTask.UsePreviousTaskArtifactsFromJobTaskId;
            dbJobTask.Type = jobTask.Type;
            dbJobTask.Parallel = jobTask.Parallel;
            dbJobTask.Settings = jobTask.Settings;
            dbJobTask.ServerId = jobTask.ServerId;

            await _dataContext.SaveChangesAsync(cancellationToken);

            try
            {
                var server = await _dataContext.Servers.FirstOrDefaultAsync(m => m.ServerId == jobTask.ServerId, cancellationToken);
                await _remoteService.UpdateJobTask(server, jobTask, cancellationToken);
            }
            catch
            {
                // ignored
            }
        }
    }
}