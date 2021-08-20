using System.Collections.Generic;
using UnityEngine;

namespace Syrus.Plugins.ChartEditor
{
    /// <summary>
    /// Allows scaling of labels up or down
    /// </summary>
    public class SetLabelScaleOption : ChartOption
    {
        private float scalar;
        
        public SetLabelScaleOption(float scalar) : base(1)
        {
            this.scalar = scalar;
        }

        public override void ApplyOption()
        {
            GUIChartEditor.CurrentChart.labelScale = scalar;
        }
    }
}