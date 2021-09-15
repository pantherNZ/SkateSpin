using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

class OptionsPageHandler : IBasePage
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
    float bottomHeight;

    private void Start()
    {
        bottomHeight = optionsPanel.anchoredPosition.y;

        var trickSelector = FindObjectOfType<TrickSelectorPage>();
        foreach( var toggle in toggles )
            toggle.toggle.onValueChanged.AddListener( ( value ) => trickSelector.ToggleCategory( toggle.toggle, toggle.category ) );
    }

    bool dragging;
    bool pointerDown;

    public void StartDrag()
    {
        dragging = true;
        pointerDown = false;
    }

    public void StopDrag()
    {
        dragging = false;
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
            optionsPanel.anchoredPosition = optionsPanel.anchoredPosition.SetY( optionsPanel.anchoredPosition.y + direction * Time.deltaTime * speed );
            yield return null;
        }

        optionsPanel.anchoredPosition = optionsPanel.anchoredPosition.SetY( toHeight );
    }
}
