using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


public enum MessageTag
{
   Network,
   PlayerCommand,
   AI
}

public enum MessageDeliveryType
{
   Immediate,
   FixedUpdate,
   Update,
   LateUpdate,
   EndOfFrame
}

[System.Serializable]
public class MessageHeader
{
     public GameObject from;
     public GameObject to;
     public bool requireReceiver = false;
     public float deliveryTime = -1.0f;
     public int deliveryFrame = -1;
     public List<MessageTag> tags;
     public MessageDeliveryType deliveryType = MessageDeliveryType.Immediate; 

     public MessageHeader(GameObject f = null)
     {
        if(f != null)
            from = f;
     }
 }

public class Message
{
     public MessageHeader header;
     public float sentTime = -1.0f;
     public int sentFrame = -1;
     public float desiredArrivalTime;
     public int desiredArrivalFrame;

     public Message(MessageHeader h = null)
     {
        if(h == null)
        {
            GameObject from = new GameObject();
            h = new MessageHeader();
            h.from = from;
            header = h; 
            from.name = "Headless Message";  
            from.hideFlags = HideFlags.HideAndDontSave; 
            GameObject.Destroy(from, 0.5f);
        }
        else
            header = h;
     }
}

[System.Serializable]
public class MessageSubscription<T>
{
     public GameObject from;
     public GameObject to;
     public bool hidden = false;
     public List<MessageTag> tags;
     public Action<T> receiver;
}

public class DelayedMessageList
{
     //public Hashtable messages = new Hashtable();
     public List<Message> messages = new List<Message>();
     public Ravens ravens;
     float nextMessageTime = 0;
     int nextMessageFrame = -1;

     public void Add<T>(T message) where T : Message
     {

         UpdateNextMessageInfo(message);
         messages.Add(message);
     }

     public DelayedMessageList(Ravens r)
     {
         ravens = r;
     }

     public void SendDelayedMessages()
     {
         if (nextMessageTime <= Time.time || (nextMessageFrame != -1 && nextMessageFrame <= Time.frameCount))
         {
             nextMessageTime = Mathf.Infinity;
             nextMessageFrame = -1;
             List<Message> removeMessages = new List<Message>();
             foreach (Message message in messages)
             {
                 if(ProcessDelayedMessage(message))
                 {
                     removeMessages.Add(message);
                 }
             }
             foreach(Message message in removeMessages)
             {
                 messages.Remove(message);
             }
         }
     }

     bool ProcessDelayedMessage<T>(T message) where T : Message
     {
         bool removeMessage = false;
         if(MessageIsDeliverable(message))
         {
             ravens.Deliver(message);
             removeMessage = true;
         }
         else
         {
             UpdateNextMessageInfo(message);
         }
         return removeMessage;
     }

     bool MessageIsDeliverable<T>(T message) where T : Message
     {
         bool isDeliverable = false;
         if (message.header.deliveryTime != -1.0)
         {
               isDeliverable = (message.desiredArrivalTime <= Time.time);
         }
         else
         {
             // deliveryFrame is only used if deliveryTime is -1
             isDeliverable = (message.desiredArrivalFrame <= Time.frameCount);
         }
         return isDeliverable;

     }

     void UpdateNextMessageInfo<T>(T message) where T : Message
     {
         if (message.header.deliveryTime != -1.0f)
         {
             nextMessageTime = (message.desiredArrivalTime < nextMessageTime) ? message.desiredArrivalTime : nextMessageTime;
         }
         else
         {
           // deliveryFrame is only used if deliveryTime is -1
             nextMessageFrame = (nextMessageFrame == -1 ||
               message.desiredArrivalFrame < nextMessageFrame) ?
             message.desiredArrivalFrame : nextMessageFrame;
         }
     }
}


public class Ravens : MonoBehaviour
{
     public int SubCount = 0;
     public Hashtable subscriptionTable = new Hashtable();
     DelayedMessageList updateMessages;
     DelayedMessageList fixedUpdateMessages;
     DelayedMessageList lateUpdateMessages;
     DelayedMessageList endOfFrameMessages;

     void Awake()
     {
         updateMessages = new DelayedMessageList(this);
         fixedUpdateMessages = new DelayedMessageList(this);
         lateUpdateMessages = new DelayedMessageList(this);
         endOfFrameMessages = new DelayedMessageList(this);
         //StartCoroutine(EndOfFrame());
     }

     public static void Subscribe<T>(MessageSubscription<T> sub)
     {
        Instance.InternalSubscribe(sub);
     }

     public static MessageSubscription<T> Subscribe<T>(Action<T> receiver)
     {
        MessageSubscription<T> sub = new MessageSubscription<T>();
        sub.receiver = receiver;
        Instance.InternalSubscribe(sub);
        return sub;
     }

     void InternalSubscribe<T>(MessageSubscription<T> sub)
     {
         if(subscriptionTable[typeof(T)] == null)
             subscriptionTable[typeof(T)] = new List<MessageSubscription<T>>();
		((List<MessageSubscription<T>>)subscriptionTable[typeof(T)]).Add(sub);

         SubCount++;
         gameObject.name = "[Ravens] (Subscribers: " + SubCount + ")";
     }

     public static void UnSubscribe<T>(MessageSubscription<T> sub)
     {
         if(!applicationIsQuitting)
         Instance.StartCoroutine(Instance.InternalUnSubscribe(sub));
     }

     public IEnumerator InternalUnSubscribe<T>(MessageSubscription<T> sub)
     {
     	yield return new WaitForEndOfFrame ();
        bool removed = true;
        if(subscriptionTable[typeof(T)] == null)
        {
            removed = false;
            Debug.Log("Trying to remove subscriber but none of that type found");
            yield return null;
        }
        if(!((List<MessageSubscription<T>>)subscriptionTable[typeof(T)]).Remove(sub))
        {
            removed = false;
            Debug.Log("Trying to remove subscriber but it was not found");
            yield return null;
        }
        if(removed)
        {
            SubCount--;
            gameObject.name = "[Ravens] (Subscribers: " + SubCount + ")";
        }
     }

     public static void Send<T>(T message) where T : Message
     {
         Instance.InternalSend(message);
     }

     public void InternalSend<T>(T message) where T : Message
     {
         if (message.header == null || message.header.from == null)
         {
           Debug.LogError("Message must include header and header.from", this);
           return;
         }

         message.sentTime = Time.time;
         message.sentFrame = Time.frameCount;
         message.desiredArrivalTime = (message.header.deliveryTime != -1) ? message.header.deliveryTime + Time.time : Time.time;
         message.desiredArrivalFrame = (message.header.deliveryFrame != -1) ? message.header.deliveryFrame + Time.frameCount : Time.frameCount;

         switch(message.header.deliveryType)
         {
             case MessageDeliveryType.Immediate:
                 Deliver(message);
             break;
             case MessageDeliveryType.Update:
                 updateMessages.Add(message);
             break;
             case MessageDeliveryType.FixedUpdate:
                 fixedUpdateMessages.Add(message);
             break;
             case MessageDeliveryType.LateUpdate:
                 lateUpdateMessages.Add(message);
             break;
             case MessageDeliveryType.EndOfFrame:
                 endOfFrameMessages.Add(message);
             break;
         }
     }

     public void Deliver<T>(T message) where T : Message
     {
         bool recivedByType = false;

         if(typeof(T) != typeof(Message))
         {
             recivedByType = DeliverWithList<T>(message,((List<MessageSubscription<T>>)subscriptionTable[typeof(T)]));
         }

         bool recivedByBase = DeliverWithList<Message>(message,((List<MessageSubscription<Message>>)subscriptionTable[typeof(Message)]));

         if(message.header.requireReceiver && !(recivedByType||recivedByBase))
             Debug.LogError("Message sent without the required receiver: " + message);
     }

     public bool DeliverWithList<T>(T message, List<MessageSubscription<T>> subs) where T : Message
     {
         bool recived = false;
         if(subs != null)
         {
             foreach(MessageSubscription<T> sub in subs)
             {
  				if(SubscriptionIncludesMessage<T>(sub,message))
                 {
                     sub.receiver(message);
                     if (!sub.hidden)  recived = true;
                 }
             }
         }
         return recived;
     }

     public bool SubscriptionIncludesMessage<T>(MessageSubscription<T> sub, T message) where T : Message
     {
         MessageHeader header = message.header;
         return  ( sub.from == null || sub.from == header.from ) &&
                 ( sub.to == null || sub.to == header.to);
     }

     void Update()
     {
         updateMessages.SendDelayedMessages();
     }

     void FixedUpdate()
     {
         fixedUpdateMessages.SendDelayedMessages();
     }

     void LateUpdate()
     {
         lateUpdateMessages.SendDelayedMessages();
     }

     IEnumerator EndOfFrame()
     {
         while (true)
         {
             yield return new WaitForEndOfFrame();
             endOfFrameMessages.SendDelayedMessages();
         }
     }

     public void OnDestroy ()
     {
         applicationIsQuitting = true;
     }

     private static Ravens instance;
     private static object _lock = new object();
     private static bool applicationIsQuitting = false;

     public static Ravens Instance
     {
        get
        {
            if (applicationIsQuitting)
            {
                Debug.LogWarning("[Ravens] Instance already destroyed on application quit." + " Won't create again - returning null.");
                return null;
            }

            lock(_lock)
            {
                if (instance == null)
                {
                    instance = (Ravens) FindObjectOfType(typeof(Ravens));

                    if ( FindObjectsOfType(typeof(Ravens)).Length > 1 )
                    {
                        Debug.LogError("[Ravens] Something went really wrong " +
                            " - there should never be more than 1 singleton!" +
                            " Reopenning the scene might fix it.");
                        return instance;
                    }

                    if (instance == null)
                    {
                        GameObject ravens = new GameObject();
                        instance = ravens.AddComponent<Ravens>();
                        ravens.name = "Ravens";
                        //DontDestroyOnLoad(ravens);
                        Debug.Log("[Ravens] An instance of Ravens is needed in the scene, so one was created");
                    }
                    else
                    {
                        Debug.Log("[Ravens] Using instance already created: " + instance.gameObject.name);
                    }
                }

                return instance;
            }
        }
     }
}
