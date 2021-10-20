using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChallengesPage : IBasePage, IEventReceiver
{
    [SerializeField] VerticalLayoutGroup verticalLayout = null;
    [SerializeField] GameObject challengeEntryPrefab = null;
    [SerializeField] GameObject challengeSubEntryPrefab = null;

    private Dictionary<DataHandler.ChallengeData, GameObject> challengeToUIEntry = new Dictionary<DataHandler.ChallengeData, GameObject>();

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
        else if( e is ChallengeCompletedEvent challengeCompleted )
        {
            UpdateCompleted( challengeCompleted.challenge );
        }
    }

    private void Initialise()
    {
        foreach( var( hash, challenges ) in DataHandler.Instance.ChallengesData )
        {
            var entry = Instantiate( challengeEntryPrefab );
            entry.GetComponentInChildren<Text>().text = challenges[0].name;
            entry.transform.SetParent( verticalLayout.transform );

            foreach( var( _, challenge ) in Utility.Enumerate( challenges ) )
            {
                var subEntry = Instantiate( challengeSubEntryPrefab );
                var text = subEntry.GetComponentInChildren<Text>();
                text.text = "Defend against " + challenge.person;
                subEntry.transform.SetParent( verticalLayout.transform );

                challengeToUIEntry.Add( challenge, subEntry );
                UpdateCompleted( challenge );
            }
        }
    }

    private void UpdateCompleted( DataHandler.ChallengeData challenge )
    {
        var thisChallenge = challenge;
        var subEntry = challengeToUIEntry[thisChallenge];
        var strikethrough = subEntry.GetComponentInChildren<Image>( true );
        var text = subEntry.GetComponentInChildren<Text>();
        strikethrough.gameObject.SetActive( thisChallenge.completed );
        subEntry.GetComponentInChildren<Button>().onClick.RemoveAllListeners();

        text.color = challenge.completed ? new Color( 1.0f, 93.0f / 255.0f, 93.0f / 255.0f ) : Color.white;

        if( thisChallenge.completed )
        {
            strikethrough.transform.localScale = strikethrough.transform.localScale.SetX( Utility.GetTextWidth( text ) / 100.0f );
        }
        else
        {
            subEntry.GetComponentInChildren<Button>().onClick.AddListener( () =>
            {
                EventSystem.Instance.TriggerEvent( new StartChallengeRequestEvent() { challenge = thisChallenge } );
                EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = 0 } );
            } );
        }
    }
}
