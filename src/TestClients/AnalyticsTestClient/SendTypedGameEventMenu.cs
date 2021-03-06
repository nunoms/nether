// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using Nether.Analytics.GameEvents;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AnalyticsTestClient.Utils
{
    public class SendTypedGameEventMenu : ConsoleMenu
    {
        public SendTypedGameEventMenu()
        {
            Title = "Nether Analytics Test Client - Send Static Game Events";

            var gameEventTypes = GetGameEventTypes();
            var menuKey = 'a';
            foreach (var gameEventType in gameEventTypes)
            {
                var gameEvent = (IGameEvent)Activator.CreateInstance(gameEventType);
                MenuItems.Add(menuKey++,
                    new ConsoleMenuItem(
                        $"{gameEvent.Type}, {gameEvent.Version}",
                        () => StaticGameEventSelected(gameEvent)));
            }
        }

        private static Type[] GetGameEventTypes()
        {
            var assembly = typeof(IGameEvent).GetTypeInfo().Assembly;
            var types = assembly.GetTypes();
            var gameEventTypes =
                from t in types
                where typeof(IGameEvent).IsAssignableFrom(t) && t != typeof(IGameEvent)
                orderby t.Name
                select t;

            return gameEventTypes.ToArray();
        }

        private void StaticGameEventSelected(IGameEvent gameEvent)
        {
            EditGameEventProperties(gameEvent);

            // Serialize object to JSON

            var msg = JsonConvert.SerializeObject(
                gameEvent,
                Formatting.Indented,
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

            //var msg = JsonConvert.SerializeObject(gameEvent);
            EventHubManager.SendMessageToEventHub(msg).Wait();
        }

        private void EditGameEventProperties(IGameEvent gameEvent)
        {
            gameEvent.ClientUtcTime = DateTime.UtcNow;

            var props = gameEvent.GetType().GetProperties();

            foreach (var prop in props)
            {
                var propName = prop.Name;
                if (propName != "ClientUtcTime" && Program.PropertyCache.ContainsKey(propName))
                {
                    prop.SetValue(gameEvent, Program.PropertyCache[propName]);
                }
                var propValue = prop.GetValue(gameEvent);

                if (propName == "Type" || propName == "Version")
                {
                    // Don't ask for input for these properties, just display their 
                    // values since they are static and can't be changed
                    Console.WriteLine($"{propName}: {propValue}");
                }
                else
                {
                    var propertyType = prop.PropertyType;
                    var o = EditProperty(propName, propValue, propertyType);
                    prop.SetValue(gameEvent, o);
                    Program.PropertyCache[propName] = o;
                }
            }
        }
    }
}