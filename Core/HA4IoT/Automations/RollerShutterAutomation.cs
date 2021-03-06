﻿using System;
using System.Collections.Generic;
using System.Linq;
using HA4IoT.Actuators.RollerShutters;
using HA4IoT.Conditions;
using HA4IoT.Conditions.Specialized;
using HA4IoT.Contracts.Actuators;
using HA4IoT.Contracts.Components;
using HA4IoT.Contracts.Components.Commands;
using HA4IoT.Contracts.Core;
using HA4IoT.Contracts.Environment;
using HA4IoT.Contracts.Notifications;
using HA4IoT.Contracts.Resources;
using HA4IoT.Contracts.Scheduling;
using HA4IoT.Contracts.Services;
using HA4IoT.Contracts.Settings;

namespace HA4IoT.Automations
{
    public class RollerShutterAutomation : AutomationBase
    {
        private readonly List<string> _rollerShutters = new List<string>();

        private readonly INotificationService _notificationService;
        private readonly IDateTimeService _dateTimeService;
        private readonly IDaylightService _daylightService;
        private readonly IOutdoorService _outdoorService;
        private readonly IComponentRegistryService _componentRegistry;
        private readonly ISettingsService _settingsService;

        private bool _maxOutsideTemperatureApplied;
        private bool _autoOpenIsApplied;
        private bool _autoCloseIsApplied;

        public RollerShutterAutomation(
            string id, 
            INotificationService notificationService,
            ISchedulerService schedulerService,
            IDateTimeService dateTimeService,
            IDaylightService daylightService,
            IOutdoorService outdoorTemperatureService,
            IComponentRegistryService componentRegistry,
            ISettingsService settingsService,
            IResourceService resourceService)
            : base(id)
        {
            if (resourceService == null) throw new ArgumentNullException(nameof(resourceService));

            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
            _daylightService = daylightService ?? throw new ArgumentNullException(nameof(daylightService));
            _outdoorService = outdoorTemperatureService ?? throw new ArgumentNullException(nameof(outdoorTemperatureService));
            _componentRegistry = componentRegistry ?? throw new ArgumentNullException(nameof(componentRegistry));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
           
            resourceService.RegisterText(
                RollerShutterAutomationNotification.AutoClosingDueToHighOutsideTemperature, 
                "Closing roller shutter because outside temperature reaches {AutoCloseIfTooHotTemperaure}°C.");

            settingsService.CreateSettingsMonitor<RollerShutterAutomationSettings>(this, s => Settings = s.NewSettings);

            schedulerService.Register(id, TimeSpan.FromMinutes(1), () => PerformPendingActions());
        }

        public RollerShutterAutomationSettings Settings { get; private set; }

        public RollerShutterAutomation WithRollerShutters(params IRollerShutter[] rollerShutters)
        {
            if (rollerShutters == null) throw new ArgumentNullException(nameof(rollerShutters));

            _rollerShutters.AddRange(rollerShutters.Select(rs => rs.Id));
            return this;
        }

        public void PerformPendingActions()
        {
            if (!Settings.IsEnabled)
            {
                return;
            }

            if (!_maxOutsideTemperatureApplied && TooHotIsAffected())
            {
                _maxOutsideTemperatureApplied = true;
                _notificationService.CreateInformation(RollerShutterAutomationNotification.AutoClosingDueToHighOutsideTemperature, Settings);
                InvokeCommand(new MoveDownCommand());

                return;
            }

            // TODO: Add check for heavy hailing

            var autoOpenIsInRange = GetIsDayCondition().IsFulfilled();
            var autoCloseIsInRange = !autoOpenIsInRange;

            if (!_autoOpenIsApplied && autoOpenIsInRange)
            {
                PerformPendingSunriseActions();
            }
            else if (!_autoCloseIsApplied && autoCloseIsInRange)
            {
                PerformPendingSunsetActions();
            }
        }

        private void PerformPendingSunriseActions()
        {
            if (DoNotOpenDueToTimeIsAffected())
            {
                return;
            }

            if (TooColdIsAffected())
            {
                _notificationService.CreateInfo($"Cancelling opening '{GetRollerShutterNames()}' because outside temperature is lower than {Settings.SkipIfFrozenTemperature}°C'.");

                _autoOpenIsApplied = true;
                _autoCloseIsApplied = false;

                return;
            }

            if (TooHotIsAffected())
            {
                _notificationService.CreateInfo($"Cancelling opening '{GetRollerShutterNames()}' because outside temperature is higher than {Settings.AutoCloseIfTooHotTemperaure}°C.");

                _autoOpenIsApplied = true;
                _autoCloseIsApplied = false;
                _maxOutsideTemperatureApplied = true;

                return;
            }

            if (Settings.SkipNextOpenOnSunrise)
            {
                Settings.SkipNextOpenOnSunrise = false;
                _settingsService.SetSettings(this, Settings);
                _notificationService.CreateInfo($"Skipped opening '{GetRollerShutterNames()}' due to sunrise this time.");
            }
            else
            {
                if (Settings.AutoOpenIsEnabled)
                {
                    InvokeCommand(new MoveUpCommand());
                    _notificationService.CreateInfo($"Opening '{GetRollerShutterNames()}' due to sunrise.");
                }
            }

            _autoOpenIsApplied = true;
            _autoCloseIsApplied = false;

            _maxOutsideTemperatureApplied = false;
        }

        private string GetRollerShutterNames()
        {
            var captions = new List<string>();
            foreach (var rollerShutterId in _rollerShutters)
            {
                var settings = _settingsService.GetComponentSettings<RollerShutterSettings>(rollerShutterId);
                captions.Add(settings.Caption);
            }

            return string.Join(", ", captions);
        }

        private void PerformPendingSunsetActions()
        {
            if (TooColdIsAffected())
            {
                _notificationService.CreateInfo($"Cancelling closing '{GetRollerShutterNames()}' because outside temperature is lower than {Settings.SkipIfFrozenTemperature}°C.");
            }
            else
            {
                if (Settings.SkipNextCloseOnSunset)
                {
                    Settings.SkipNextCloseOnSunset = false;
                    _settingsService.SetSettings(this, Settings);
                    _notificationService.CreateInfo($"Skipped closing '{GetRollerShutterNames()}' due to sunrise this time.");
                }
                else
                {
                    if (Settings.AutoCloseIsEnabled)
                    {
                        InvokeCommand(new MoveDownCommand());
                        _notificationService.CreateInfo($"Closed '{GetRollerShutterNames()}' due to sunset.");
                    }
                }
            }

            _autoCloseIsApplied = true;
            _autoOpenIsApplied = false;
        }

        private bool DoNotOpenDueToTimeIsAffected()
        {
            if (Settings.SkipBeforeTimestampIsEnabled &&
                Settings.SkipBeforeTimestamp > _dateTimeService.Time)
            {
                return true;
            }

            return false;
        }

        private bool TooHotIsAffected()
        {
            if (Settings.AutoCloseIfTooHotIsEnabled && 
                _outdoorService.Temperature > Settings.AutoCloseIfTooHotTemperaure)
            {
                return true;
            }

            return false;
        }

        private bool TooColdIsAffected()
        {
            if (Settings.SkipIfFrozenIsEnabled &&
                _outdoorService.Temperature < Settings.SkipIfFrozenTemperature)
            {
                return true;
            }

            return false;
        }

        private IsDayCondition GetIsDayCondition()
        {
            var condition = new IsDayCondition(_daylightService, _dateTimeService);
            condition.WithStartAdjustment(Settings.OpenOnSunriseOffset);
            condition.WithEndAdjustment(Settings.CloseOnSunsetOffset);

            return condition;
        }

        private void InvokeCommand(ICommand command)
        {
            foreach (var rollerShutter in _rollerShutters)
            {
                _componentRegistry.GetComponent(rollerShutter).ExecuteCommand(command);
            }
        }
    }
}
