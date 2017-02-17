﻿using System;
using System.Collections.Generic;
using HA4IoT.Contracts.Api;
using HA4IoT.Contracts.Commands;
using HA4IoT.Contracts.Components;
using HA4IoT.Contracts.Logging;
using Newtonsoft.Json.Linq;

namespace HA4IoT.Components
{
    public abstract class ComponentBase : IComponent
    {
        protected ComponentBase(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));

            Id = id;
        }

        public event EventHandler<ComponentFeatureStateChangedEventArgs> StateChanged;

        public string Id { get; }

        public abstract ComponentFeatureStateCollection GetState();

        public abstract ComponentFeatureCollection GetFeatures();
        
        public abstract void InvokeCommand(ICommand command);

        protected void OnStateChanged(ComponentFeatureStateCollection oldState)
        {
            OnStateChanged(oldState, GetState());
        }

        protected void OnStateChanged(ComponentFeatureStateCollection oldState, ComponentFeatureStateCollection newState)
        {
            var oldStateText = oldState?.Serialize();
            var newStateText = newState?.Serialize();

            Log.Info($"Component '{Id}' update state ' from '{oldStateText}' to '{newStateText}'");
            StateChanged?.Invoke(this, new ComponentFeatureStateChangedEventArgs(oldState, newState));
        }


        #region OLD

        public virtual IList<GenericComponentState> GetSupportedStates()
        {
            return new List<GenericComponentState>();
        }

        public virtual void HandleApiCall(IApiContext apiContext)
        {
        }

        public virtual JToken ExportStatus()
        {
            var status = new JObject
            {
                ["State"] = JObject.FromObject(GetState().Serialize())
            };

            return status;
        }
        #endregion


    }
}