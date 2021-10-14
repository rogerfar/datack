﻿using System;

namespace Datack.Agent.Models
{
    public class CompleteEvent
    {
        public Guid StepLogId { get; set; }
        public Guid JobLogId { get; set; }
        public String Message { get; set; }
        public Boolean IsError { get; set; }
    }
}
