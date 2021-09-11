using UnityEngine;

class MinMaxSlider : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Slider sliderMin = null;
    [SerializeField] private UnityEngine.UI.Slider sliderMax = null;
    private bool editing = false;

    private void Start()
    {
        sliderMax.minValue = sliderMin.minValue;
        sliderMax.maxValue = sliderMin.maxValue;

        sliderMin.onValueChanged.AddListener( ( float value ) =>
        {
            if( editing )
                return;

            editing = true;
            sliderMin.value = Mathf.Min( value, sliderMax.value );
            editing = false;
        } );

        sliderMax.onValueChanged.AddListener( ( float value ) =>
        {
            if( editing )
                return;

            editing = true;
            sliderMax.value = Mathf.Max( value, sliderMin.value );
            editing = false;
        } );
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
