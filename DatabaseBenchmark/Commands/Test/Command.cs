﻿using DatabaseBenchmark.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseBenchmark.Commands
{
    public abstract class Command
    {
        public Database[] Databases { get; set; }

        public abstract void Execute();
    }
}