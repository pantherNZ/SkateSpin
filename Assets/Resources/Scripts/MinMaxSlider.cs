using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

class MinMaxSlider : MonoBehaviour
{
    [SerializeField] private Slider sliderMin = null;
    [SerializeField] private Slider sliderMax = null;
    [SerializeField] RectTransform fill = null;
    [SerializeField] RectTransform minHandle = null;
    [SerializeField] RectTransform maxHandle = null;
    private bool editing = false;
    private Text minText;
    private Text maxText;

    private void Start()
    {
        minText = minHandle.GetComponentInChildren<Text>();
        maxText = maxHandle.GetComponentInChildren<Text>();

        sliderMax.minValue = sliderMin.minValue;
        sliderMax.maxValue = sliderMin.maxValue;

        sliderMin.onValueChanged.AddListener( ( float value ) =>
        {
            if( !editing )
            {
                editing = true;
                sliderMin.value = Mathf.Min( value, sliderMax.value );
                editing = false;
                OnValueChanged();
            }
        } );

        sliderMax.onValueChanged.AddListener( ( float value ) =>
        {
            if( !editing )
            {
                editing = true;
                sliderMax.value = Mathf.Max( value, sliderMin.value );
                editing = false;
                OnValueChanged();
            }
        } );
    }

    private void OnValueChanged()
    {
        if( fill != null )
        {
            var previousPosition = fill.localPosition;
            fill.anchorMin = minHandle.anchorMin;
            fill.anchorMax = maxHandle.anchorMax;
            fill.offsetMin = new Vector2( minHandle.anchoredPosition.x, 0.0f );
            fill.offsetMax = new Vector2( maxHandle.anchoredPosition.x, 0.0f );

            foreach( Transform child in fill )
                child.localPosition -= ( fill.localPosition - previousPosition );
        }

        if( minText != null )
            minText.text = Mathf.RoundToInt( sliderMin.value ).ToString();
        if( maxText != null )
            maxText.text = Mathf.RoundToInt( sliderMax.value ).ToString();
    }

    public float GetMinValue()
    {
        return sliderMin.value;
    }

    public float GetMaxValue()
    {
        return sliderMax.value;
    }
}
