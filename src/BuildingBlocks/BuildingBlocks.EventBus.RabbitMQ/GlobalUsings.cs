global using System;
global using System.Text.Json;
global using System.Threading;
global using System.Threading.Tasks;

global using BuildingBlocks.EventBus.Abstractions;
global using BuildingBlocks.EventBus.Subscriptions;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

global using RabbitMQ.Client;
global using RabbitMQ.Client.Events;