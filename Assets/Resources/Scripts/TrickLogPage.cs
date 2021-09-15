using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrickLogPage : IBasePage
{
    [SerializeField] List<Image> progressCircles = new List<Image>();

    public override void OnShown()
    {
        Dictionary<string, Pair<int, int>> landedData = new Dictionary<string, Pair<int, int>>();

        foreach( var x in DataHandler.Instance.trickData )
        {
            if( landedData.TryGetValue( x.category, out var oldValue ) )
                landedData[x.category] = new Pair<int, int>( oldValue.First + ( x.landed ? 1 : 0 ), oldValue.Second + 1 );
            else
                landedData.Add( x.category, new Pair<int, int>( x.landed ? 1 : 0, 1 ) );
        }

        foreach( var x in progressCircles )
        {
            var category = x.GetComponentsInChildren<Text>()[1].text;

            if( !DataHandler.Instance.categories.Contains( category ) )
            {
                Debug.LogError( string.Format( "Progress circle category is not valid: {0} + ({1})", category, x.name ) );
                continue;
            }

            var value = landedData.ContainsKey( category ) ? landedData[category].First / ( float )landedData[category].Second : 0.0f; // Random.value;
            x.GetComponentsInChildren<Image>()[1].fillAmount = value;
            x.GetComponentInChildren<Text>().text = Mathf.RoundToInt( value * 100.0f ).ToString() + "%";
        }
    }
}
