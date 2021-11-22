using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrickLogPage : IBasePage, IEventReceiver
{
    [SerializeField] List<Image> progressCircles = new List<Image>();
    TrickSelectorPage trickSelector;

    private void Awake()
    {
        trickSelector = FindObjectOfType<TrickSelectorPage>();

        EventSystem.Instance.AddSubscriber( this ); 
    }

    private void UpdateProgessCircles()
    {
        var landedData = trickSelector.LandedData;

        foreach( var x in progressCircles )
        {
            var category = x.GetComponentsInChildren<Text>()[1].text;

            if( !DataHandler.Instance.Categories.Contains( category ) )
            {
                Debug.LogError( string.Format( "Progress circle category is not valid: {0} + ({1})", category, x.name ) );
                continue;
            }

            var value = ( ( float )landedData[category].landed ).SafeDivide( ( float )landedData[category].total );
            //var value = Random.value;
            x.GetComponentsInChildren<Image>()[1].fillAmount = value;
            x.GetComponentInChildren<Text>().text = Mathf.RoundToInt( value * 100.0f ).ToString() + "%";
        }
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is TrickLandedEvent 
            || e is TrickDifficultyChangedEvent
            || e is DataLoadedEvent
            || e is ResetSaveDataEvent )
        {
            UpdateProgessCircles();
        }
    }
}
