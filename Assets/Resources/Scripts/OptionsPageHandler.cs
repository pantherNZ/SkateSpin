using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

class OptionsPageHandler : MonoBehaviour
{
    [SerializeField] private RectTransform optionsPanel = null;
    [SerializeField] float topHeight = -550.0f;
    [SerializeField] float moveTimeSec = 0.25f;
    private float bottomHeight;

    private void Start()
    {
        GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        bottomHeight = optionsPanel.anchoredPosition.y;
    }

    private bool dragging;
    private bool pointerDown;

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
