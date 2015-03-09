using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RavenMessages
{

    /// <summary>
    /// The message sent when the PositionController has reached it's target you can
    /// include a string message for the reciever.
    /// </summary>
    [System.Serializable]
    public class HotspotValueChanged : Message
    {
        public string Address{get; set;}
        public bool Value { get; set; }
        public HotspotValueChanged(MessageHeader h, string address, bool value)
            : base(h) 
        {
            Address = address;
            Value = value;

        }
    }



}
