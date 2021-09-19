using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrickListPage : IBasePage, IEventReceiver
{
    [SerializeField] VerticalLayoutGroup verticalLayout = null;
    [SerializeField] GameObject categoryHeadingPrefab = null;
    [SerializeField] GameObject difficultyHeadingPrefab = null;
    [SerializeField] GameObject trickEntryPrefab = null;
    TrickSelectorPage trickSelector;
    readonly Dictionary<string, List<Pair<bool, Text>>> difficultyEntryData = new Dictionary<string, List<Pair<bool, Text>>>();
    readonly Dictionary<DataHandler.TrickEntry, TextMeshProUGUI> trickEntries = new Dictionary<DataHandler.TrickEntry, TextMeshProUGUI>();

    private bool difficultyCallbackEnabled = true;

    private void Awake()
    {
        EventSystem.Instance.AddSubscriber( this );
    }

    public void OnEventReceived( IBaseEvent e )
    {
        if( e.GetType() == typeof( DataLoadedEvent ) )
            Initialise();
    }

    private void Initialise()
    {
        trickSelector = FindObjectOfType<TrickSelectorPage>();

        foreach( Transform child in verticalLayout.transform )
            child.gameObject.Destroy();

        foreach( var category in DataHandler.Instance.Categories )
        {
            var difficultyChildIndices = new List<int>();
            var perDifficultyData = DataHandler.Instance.TrickData[category];

            var categoryHeading = Instantiate( categoryHeadingPrefab );
            categoryHeading.transform.SetParent( verticalLayout.transform, false );
            categoryHeading.GetComponentInChildren<Text>().text = category;

            var categoryStartIdx = verticalLayout.transform.childCount;
            int difficultyIdx = 0;

            var categoryInfo = difficultyEntryData.GetOrAdd( category );
            var buttons = new List<Button>();

            foreach( var (difficulty, name) in DataHandler.Instance.DifficultyNames )
            {
                difficultyChildIndices.Add( verticalLayout.transform.childCount );

                var difficultyHeading = Instantiate( difficultyHeadingPrefab );
                difficultyHeading.SetActive( false );
                difficultyHeading.transform.SetParent( verticalLayout.transform, false );
                var texts = difficultyHeading.GetComponentsInChildren<Text>();
                texts[0].text = string.Format( "Difficulty - {0} ({1})",
                        difficulty,
                        DataHandler.Instance.DifficultyNames[difficulty] );

                categoryInfo.Add( new Pair<bool, Text>( false, texts[1] ) );

                var difficultyStartIdx = verticalLayout.transform.childCount;

                // Create trick entries
                foreach( var trick in perDifficultyData[difficulty] )
                {
                    var trickEntry = Instantiate( trickEntryPrefab );
                    trickEntry.SetActive( false );
                    trickEntry.transform.SetParent( verticalLayout.transform, false );
                    var text = trickEntry.GetComponentInChildren<TextMeshProUGUI>();
                    text.text = trick.name;
                    UpdateTrickEntryVisual( text, trick );

                    trickEntry.GetComponent<Button>().onClick.AddListener( () =>
                    {
                        trick.status = ( DataHandler.TrickEntry.Status )( ( ( int )trick.status + 1 ) % ( int )DataHandler.TrickEntry.Status.MaxStatusValues );
                        UpdateTrickEntryVisual( text, trick );
                    } );

                    trickEntries.Add( trick, text );
                }

                var difficultyEndIdx = verticalLayout.transform.childCount;
                int idx = difficultyIdx;
                buttons.Add( difficultyHeading.GetComponent<Button>() );

                // Setup on click for the difficulty entry
                buttons.Back().onClick.AddListener( () =>
                {
                    if( difficultyCallbackEnabled )
                        categoryInfo[idx].First = !categoryInfo[idx].First;
                    for( int i = difficultyStartIdx; i < difficultyEndIdx; ++i )
                        verticalLayout.transform.GetChild( i ).gameObject.ToggleActive();
                } );

                ++difficultyIdx;
            }

            var categoryEndIdx = verticalLayout.transform.childCount;

            categoryHeading.GetComponent<Button>().onClick.AddListener( () =>
            {
                // Read and open any difficulty categories already open
                if( !verticalLayout.transform.GetChild( difficultyChildIndices[0] ).gameObject.activeSelf )
                {
                    difficultyCallbackEnabled = false;

                    foreach( var (_, openInfo) in difficultyEntryData )
                        foreach( var (difficultyData, button) in Utility.Zip( openInfo, buttons ) )
                            if( difficultyData.First )
                                button.onClick.Invoke();

                    foreach( var idx in difficultyChildIndices )
                        verticalLayout.transform.GetChild( idx ).gameObject.ToggleActive();

                    difficultyCallbackEnabled = true;
                }
                else
                {
                    for( int idx = categoryStartIdx; idx < categoryEndIdx; ++idx )
                        verticalLayout.transform.GetChild( idx ).gameObject.SetActive( false );
                }
            } );
        }
    }

    public void HideAllDifficultyEntries()
    {
        foreach( var (_, data) in difficultyEntryData )
            for( int i = 0; i < data.Count; ++i )
                data[i].First = false;
    }

    public override void OnShown()
    {
        var landedData = trickSelector.LandedData;

        foreach( var (category, data) in difficultyEntryData )
        {
            foreach( var (difficulty, entry) in Utility.Enumerate( data ) )
            {
                var completionData = landedData[category].perDifficultyLands;
                var completionPercent = ( ( float )completionData[difficulty + 1].First ).SafeDivide( ( float )completionData[difficulty + 1].Second );
                entry.Second.text = Mathf.RoundToInt( completionPercent * 100.0f ).ToString() + "%";
            }
        }

        foreach( var (trick, text) in trickEntries )
            UpdateTrickEntryVisual( text, trick );
    }

    private void UpdateTrickEntryVisual( TextMeshProUGUI text, DataHandler.TrickEntry trick )
    {
        text.fontStyle = trick.status == DataHandler.TrickEntry.Status.Landed ? FontStyles.Strikethrough : FontStyles.Normal;
        text.color = trick.status == DataHandler.TrickEntry.Status.Banned ? new Color( 1.0f, 1.0f, 1.0f, 0.5f ) : Color.white;
    }

    public void ExpandCategory( string category )
    {

    }
}