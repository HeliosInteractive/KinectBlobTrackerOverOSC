using UnityEngine;
using System.Collections;

public class BoxController : MonoBehaviour {
    public string ID="box1";
    // Use this for initialization
	void Start () {
        Ravens.Subscribe<RavenMessages.HotspotValueChanged>((status) => {
            if (status.Address == ID)
            {
                GetComponent<SpriteRenderer>().color = status.Value ? Color.green : Color.gray;
            }
        
        });
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
