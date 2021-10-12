using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChallengesPage : IBasePage, IEventReceiver
{
    [SerializeField] VerticalLayoutGroup verticalLayout = null;
    [SerializeField] GameObject challengeEntryPrefab = null;
    [SerializeField] GameObject challengeSubEntryPrefab = null;

    void Awake()
    {
        EventSystem.Instance.AddSubscriber( this );
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is DataLoadedEvent )
        {
            Initialise();
        }
    }

    private void Initialise()
    {
        foreach( var( name, challenges ) in DataHandler.Instance.ChallengesData )
        {
            var entry = Instantiate( challengeEntryPrefab );
            entry.GetComponentInChildren<Text>().text = name;
            entry.transform.SetParent( verticalLayout.transform );

            foreach( var challenge in challenges )
            {
                var subEntry = Instantiate( challengeSubEntryPrefab );
                var text = subEntry.GetComponentInChildren<Text>();
                text.text = "Defend against " + challenge.person;
                subEntry.transform.SetParent( verticalLayout.transform );

                var strikethrough = subEntry.GetComponentInChildren<Image>();
                strikethrough.gameObject.SetActive( challenge.completed );
                if( challenge.completed )
                    strikethrough.transform.localScale = strikethrough.transform.localScale.SetX( Utility.GetTextWidth( text ) / 100.0f );
            }
        }
    }
}
