using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrickListPage : IBasePage
{
    [SerializeField] VerticalLayoutGroup verticalLayout = null;
    [SerializeField] GameObject categoryHeadingPrefab = null;
    [SerializeField] GameObject difficultyHeadingPrefab = null;
    [SerializeField] GameObject trickEntryPrefab = null;
    TrickSelectorPage trickSelector;

    private void Start()
    {
        trickSelector = FindObjectOfType<TrickSelectorPage>();
    }

    public override void OnShown()
    {
        var landedData = trickSelector.LandedData;

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

            foreach( var (difficulty, name) in DataHandler.Instance.DifficultyNames )
            {
                difficultyChildIndices.Add( verticalLayout.transform.childCount );

                var difficultyHeading = Instantiate( difficultyHeadingPrefab );
                difficultyHeading.SetActive( false );
                difficultyHeading.transform.SetParent( verticalLayout.transform, false );
                var texts = difficultyHeading.GetComponentsInChildren<Text>();
                texts[0].text = "Difficulty - " + difficulty.ToString();

                var completionData = landedData[category].perDifficultyLands;
                var completionPercent = ( ( float )completionData[difficulty].First ).SafeDivide( ( float )completionData[difficulty].Second );
                texts[1].text = Mathf.RoundToInt( completionPercent * 100.0f ).ToString() + "%";

                var difficultyStartIdx = verticalLayout.transform.childCount;

                // Create trick entries
                foreach( var trick in perDifficultyData[difficulty] )
                {
                    var trickEntry = Instantiate( trickEntryPrefab );
                    trickEntry.SetActive( false );
                    trickEntry.transform.SetParent( verticalLayout.transform, false );
                    var text = trickEntry.GetComponentInChildren<TextMeshProUGUI>();
                    text.text = trick.displayName;
                    text.fontStyle = trick.landed ? FontStyles.Strikethrough : FontStyles.Normal;
                    text.color = trick.banned ? new Color( 1.0f, 1.0f, 1.0f, 0.5f ) : Color.white;

                    // TODO:
                    // Colour / strikethrough based on landed / banned
                    // Add button callback to iterate through banned / landed / neither


                    trickEntry.GetComponent<Button>().onClick.AddListener( () =>
                    {
                    } );
                }

                var difficultyEndIdx = verticalLayout.transform.childCount;

                // Setup on click for the difficulty entry
                difficultyHeading.GetComponent<Button>().onClick.AddListener( () =>
                {
                    for( int i = difficultyStartIdx; i < difficultyEndIdx; ++i )
                        verticalLayout.transform.GetChild( i ).gameObject.ToggleActive();
                } );
            }

            var categoryEndIdx = verticalLayout.transform.childCount;

            // TODO:
            // Have it remember which difficulty blocks were open when this category was hidden and re-open next time this category is opened

            categoryHeading.GetComponent<Button>().onClick.AddListener( () =>
            {
                if( verticalLayout.transform.GetChild( difficultyChildIndices[0] ).gameObject.activeSelf )
                {
                    for( int idx = categoryStartIdx; idx < categoryEndIdx; ++idx )
                        verticalLayout.transform.GetChild( idx ).gameObject.SetActive( false );
                }
                else
                {
                    foreach( var idx in difficultyChildIndices )
                        verticalLayout.transform.GetChild( idx ).gameObject.SetActive( true );
                }
            } );
        }
    }
}