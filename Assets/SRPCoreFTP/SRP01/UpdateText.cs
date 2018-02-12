using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UpdateText : MonoBehaviour 
{
	public InputField text;
	
	// Use this for initialization
	public void UpdateTextContent(string tx)
	{
		text.text = tx;
	}
	
	// Update is called once per frame
	void Update () 
	{
		
	}
}
