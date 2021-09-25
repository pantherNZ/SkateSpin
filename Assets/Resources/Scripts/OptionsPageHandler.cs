using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

class OptionsPageHandler : IBasePage, IEventReceiver
{
    [Serializable]
    public class CategoryButton
    {
        public Toggle toggle = null;
        public string category = string.Empty;
    }

    [SerializeField] RectTransform optionsPanel = null;
    [SerializeField] float topHeight = -420.0f;
    [SerializeField] float moveTimeSec = 0.25f;
    [SerializeField] List<CategoryButton> toggles = new List<CategoryButton>();
    [SerializeField] Toggle shortTrickNamesToggle = null;
    [SerializeField] Toggle canPickLandedTricksToggle = null;

    TrickSelectorPage trickSelector;
    float bottomHeight;

    private void Awake()
    {
        EventSystem.Instance.AddSubscriber( this );
        trickSelector = FindObjectOfType<TrickSelectorPage>();
    }

    private void Start()
    {
        bottomHeight = optionsPanel.anchoredPosition.y;

        foreach( var toggle in toggles )
        {
            toggle.toggle.onValueChanged.AddListener( ( value ) =>
            {
                trickSelector.ToggleCategory( toggle.toggle, toggle.category );
            } );
        }

        shortTrickNamesToggle.onValueChanged.AddListener( ( on ) =>
        {
            EventSystem.Instance.TriggerEvent( new UseShortTrickNamesEvent() { value = on }, this );
        } );

        canPickLandedTricksToggle.onValueChanged.AddListener( ( on ) =>
        {
            EventSystem.Instance.TriggerEvent( new CanPickLandedTricksEvent() { value = on }, this );
        } );
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e.GetType() == typeof( UseShortTrickNamesEvent ) )
        {
            shortTrickNamesToggle.SetIsOnWithoutNotify( ( ( UseShortTrickNamesEvent )e ).value );
        }
        else if( e.GetType() == typeof( CanPickLandedTricksEvent ) )
        {
            canPickLandedTricksToggle.SetIsOnWithoutNotify( ( ( CanPickLandedTricksEvent )e ).value );
        }
    }

    public override void OnShown()
    {
        base.OnShown();

        foreach( var toggle in toggles )
            toggle.toggle.SetIsOnWithoutNotify( trickSelector.CurrentCategories.Contains( toggle.category ) );
    }

    //bool dragging;
    bool pointerDown;

    public void StartDrag()
    {
        //dragging = true;
        pointerDown = false;
    }

    public void StopDrag()
    {
        //dragging = false;
    }

    public void PointerDown()
    {
        pointerDown = true;
    }

    public void PointerUp()
    {
        if( pointerDown )
            ToggleOptions();
    }

    public void ToggleOptions()
    {
        StartCoroutine( MoveToHeight( optionsPanel.anchoredPosition.y < topHeight ? topHeight : bottomHeight ) );
    }

    private IEnumerator MoveToHeight( float toHeight )
    {
        float speed = Mathf.Abs( toHeight - optionsPanel.anchoredPosition.y ) / moveTimeSec;
        float direction = Mathf.Sign( toHeight - optionsPanel.anchoredPosition.y );

        while( ( direction > 0 && optionsPanel.anchoredPosition.y < toHeight ) ||
               ( direction < 0 && optionsPanel.anchoredPosition.y > toHeight ) )
        {
            var difference = Time.deltaTime * speed;
            difference = Mathf.Min( difference, Mathf.Abs( toHeight - optionsPanel.anchoredPosition.y ) );
            optionsPanel.anchoredPosition = optionsPanel.anchoredPosition.SetY( optionsPanel.anchoredPosition.y + difference * direction );
            yield return null;
        }

        optionsPanel.anchoredPosition = optionsPanel.anchoredPosition.SetY( toHeight );
    }
}
