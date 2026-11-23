using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace Warehouse
{
    public sealed class SingleSnapTurnProvider : SnapTurnProvider
    {
        bool turned;

        protected override float GetTurnAmount(Vector2 input)
        {
            if (input.sqrMagnitude < 0.09f)
                turned = false;

            if (turned || Mathf.Abs(input.x) < 0.6f)
                return 0f;

            float amount = base.GetTurnAmount(input);
            turned = amount != 0f;
            return amount;
        }
    }
}
