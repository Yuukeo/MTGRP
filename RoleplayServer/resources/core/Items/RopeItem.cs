﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using RoleplayServer.resources.inventory;

namespace RoleplayServer.resources.core.Items
{
    class RopeItem : IInventoryItem
    {
        public ObjectId Id { get; set; }

        public bool CanBeGiven => true;
        public bool CanBeDropped => true;
        public bool CanBeStashed => true;
        public bool CanBeStacked => true;

        public bool IsBlocking => false;

        public int MaxAmount => -1;

        public int AmountOfSlots => 5;

        public string CommandFriendlyName => "rope";

        public string LongName => "Rope";

        public int Object => -1237359228;

        public int Amount { get; set; }
    }
}