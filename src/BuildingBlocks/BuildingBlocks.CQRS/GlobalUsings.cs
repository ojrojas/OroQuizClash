global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Linq;
global using System.Reflection;
global using System.Threading;
global using System.Threading.Tasks;

global using BuildingBlocks.CQRS.Abstractions;
global using BuildingBlocks.CQRS.Dispatching;
global using BuildingBlocks.CQRS.Validation;
global using BuildingBlocks.Kernel.Domain.Events;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Logging;
