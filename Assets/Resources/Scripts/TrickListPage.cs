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

    class CategoryData
    {
        public Button categoryEntry;
        public bool isOpen;
        public List<int> difficultyChildIndices = new List<int>();

        public class DifficultyData
        {
            public bool wasOpen;
            public bool isOpen;
            public Text text;
            public Button button;
        }

        public List<DifficultyData> perDifficultyData = new List<DifficultyData>();
    }

    readonly Dictionary<string, CategoryData> difficultyEntryData = new Dictionary<string, CategoryData>();
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
            var perDifficultyData = DataHandler.Instance.TrickData[category];

            var categoryHeading = Instantiate( categoryHeadingPrefab );
            categoryHeading.transform.SetParent( verticalLayout.transform, false );
            categoryHeading.GetComponentInChildren<Text>().text = category;
            var categoryButton = categoryHeading.GetComponent<Button>();

            var categoryStartIdx = verticalLayout.transform.childCount;
            int difficultyIdx = 0;

            var categoryInfo = difficultyEntryData.GetOrAdd( category );
            categoryInfo.categoryEntry = categoryButton;
            var buttons = new List<Button>();

            foreach( var (difficulty, name) in DataHandler.Instance.DifficultyNames )
            {
                categoryInfo.difficultyChildIndices.Add( verticalLayout.transform.childCount );

                var difficultyHeading = Instantiate( difficultyHeadingPrefab );
                difficultyHeading.SetActive( false );
                difficultyHeading.transform.SetParent( verticalLayout.transform, false );

                var texts = difficultyHeading.GetComponentsInChildren<Text>();
                texts[0].text = string.Format( "Difficulty - {0} ({1})",
                        difficulty,
                        DataHandler.Instance.DifficultyNames[difficulty] );
                buttons.Add( difficultyHeading.GetComponent<Button>() );

                categoryInfo.perDifficultyData.Add( new CategoryData.DifficultyData()
                {
                    text = texts[1],
                    button = buttons.Back(),
                } );

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

                // Setup on click for the difficulty entry
                buttons.Back().onClick.AddListener( () =>
                {
                    if( difficultyCallbackEnabled )
                        categoryInfo.perDifficultyData[idx].wasOpen = !categoryInfo.perDifficultyData[idx].wasOpen;

                    categoryInfo.perDifficultyData[idx].isOpen = !categoryInfo.perDifficultyData[idx].isOpen;

                    for( int i = difficultyStartIdx; i < difficultyEndIdx; ++i )
                        verticalLayout.transform.GetChild( i ).gameObject.ToggleActive();
                } );

                ++difficultyIdx;
            }

            var categoryEndIdx = verticalLayout.transform.childCount;

            categoryButton.onClick.AddListener( () =>
            {
                categoryInfo.isOpen = !categoryInfo.isOpen;

                // Read and open any difficulty categories already open
                if( categoryInfo.isOpen )
                {
                    difficultyCallbackEnabled = false;

                    foreach( var (difficultyData, button) in Utility.Zip( categoryInfo.perDifficultyData, buttons ) )
                        if( difficultyData.wasOpen )
                            button.onClick.Invoke();

                    foreach( var idx in categoryInfo.difficultyChildIndices )
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

    public void ShrinkAllEntries()
    {
        difficultyCallbackEnabled = false;

        foreach( var (_, data) in difficultyEntryData )
        {
            for( int i = 0; i < data.perDifficultyData.Count; ++i )
            {
                data.perDifficultyData[i].wasOpen = false;
                if( data.perDifficultyData[i].isOpen )
                    data.perDifficultyData[i].button.onClick.Invoke();
            }

            if( data.isOpen )
                data.categoryEntry.onClick.Invoke();
        }

        difficultyCallbackEnabled = true;
    }

    public override void OnShown()
    {
        var landedData = trickSelector.LandedData;

        foreach( var (category, data) in difficultyEntryData )
        {
            foreach( var (difficulty, entry) in Utility.Enumerate( data.perDifficultyData ) )
            {
                var completionData = landedData[category].perDifficultyLands;
                var completionPercent = ( ( float )completionData[difficulty + 1].First ).SafeDivide( ( float )completionData[difficulty + 1].Second );
                entry.text.text = Mathf.RoundToInt( completionPercent * 100.0f ).ToString() + "%";
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
        ShrinkAllEntries();
        difficultyEntryData[category].categoryEntry.onClick.Invoke();
    }
}