using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;

namespace Hollan.Functions.CircuitBreaker
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CircuitEntity
    {
        // Static logger to log within methods
        private static ILogger _log;
        // The TimeSpan difference from latest to keep failures in the window
        private static readonly TimeSpan windowSize = TimeSpan.Parse(Environment.GetEnvironmentVariable("WindowSize"));
        // The number of failures in the window until closing the circuit
        private static readonly int failureThreshold = int.Parse(Environment.GetEnvironmentVariable("FailureThreshold"));

        // Current rolling window of failures reported for this circuit
        [JsonProperty]
        public IDictionary<Guid, FailureRequest> FailureWindow;

        // Current state of this circuit
        [JsonProperty]
        public CircuitState State;

        // Add a failure to this circuit.  Will add it to the dictionary and then filter the dictionary
        // based on the windowSize and evaluate if the failure threshold has been crossed. If it has
        // then the circuit will be closed 
        
        // TODO: Trigger an orchestration to disable function app when circuit closed
        public void AddFailure(FailureRequest req)
        {
            if(State == CircuitState.Closed)
            {
                _log.LogInformation($"Tried to add additional failure to {Entity.Current.EntityKey} that is already closed");
                return;
            }

            FailureWindow.Add(req.RequestId, req);

            var cutoff = req.FailureTime.Subtract(windowSize);
            FailureWindow = FailureWindow.Where(p => p.Value.FailureTime >= cutoff).ToDictionary( p => p.Key, p => p.Value);

            if(FailureWindow.Count >= failureThreshold)
            {
                _log.LogCritical($"Break this circuit for entity {Entity.Current.EntityKey}!");
                State = CircuitState.Closed;
            }
            else 
            {
                _log.LogInformation($"The circuit {Entity.Current.EntityKey} currently has {FailureWindow.Count} exceptions in the window of {windowSize.ToString()}");
            }
        }

        [FunctionName(nameof(CircuitEntity))]
        public Task HandleEntityOperation(
            [EntityTrigger] IDurableEntityContext ctx,
            ILogger log)
        {
            _log = log;
            _log.LogInformation("Entity triggered");
            // If this is the first time sending a signal to this instance
            // of a circuit breaker
            if (ctx.IsNewlyConstructed)
            {
                ctx.SetState(new CircuitEntity() 
                {
                    FailureWindow = new Dictionary<Guid, FailureRequest>(),
                    State = CircuitState.Open
                });
            }

            return ctx.DispatchAsync<CircuitEntity>();
        }

        public enum CircuitState 
        {
            Closed = 0,
            Open = 1
        }
    }
}