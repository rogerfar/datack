﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datack.Common.Models.Data;
using Datack.Web.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog.Events;

namespace Datack.Web.Web.Controllers
{
    [Authorize]
    [Route("Api/Settings")]
    public class SettingsController : Controller
    {
        private readonly Settings _settings;

        public SettingsController(Settings settings)
        {
            _settings = settings;
        }

        [HttpGet]
        [Route("")]
        public async Task<ActionResult<IList<Setting>>> Get()
        {
            var result = await _settings.GetAll();
            return Ok(result);
        }

        [HttpPut]
        [Route("")]
        public async Task<ActionResult> Update([FromBody] SettingsControllerUpdateRequest request)
        {
            await _settings.Update(request.Settings);
            
            var logLevelSetting = await _settings.Get("LogLevel");

            if (!Enum.TryParse<LogEventLevel>(logLevelSetting?.Value, out var logLevel))
            {
                logLevel = LogEventLevel.Information;
            }

            Program.LoggingLevelSwitch.MinimumLevel = logLevel;

            return Ok();
        }
    }

    public class SettingsControllerUpdateRequest
    {
        public IList<Setting> Settings { get; set; }
    }
}