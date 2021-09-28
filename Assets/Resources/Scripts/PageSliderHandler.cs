using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class PageSliderHandler : MonoBehaviour
{
    [SerializeField] private HorizontalLayoutGroup horizontalPageLayout = null;
    Vector2? dragStartPos;

    private void Update()
    {
        if( Input.GetMouseButtonDown( 0 ) )
        {
            dragStartPos = Input.mousePosition;
        }
        else if( Input.GetMouseButtonUp( 0 ) )
        {
            dragStartPos = null;
        }

        if( dragStartPos != null )
        {
            ( horizontalPageLayout.transform as RectTransform ).anchoredPosition += ( Input.mousePosition.ToVector2() - dragStartPos.Value );
            dragStartPos = Input.mousePosition;
        }
    }
}
