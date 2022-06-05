using System;

namespace ShoppingListServer.Models.Commands
{
    public class ListLastChangeTimeDTO
    {
        public string SyncId { get; set; }
        public DateTime LastChangeServerTime { get; set; }

        public ListLastChangeTimeDTO(string syncId, DateTime lastChangeServerTime)
        {
            SyncId = syncId;
            LastChangeServerTime = lastChangeServerTime;
        }
    }
}
