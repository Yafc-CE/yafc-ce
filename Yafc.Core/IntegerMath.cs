using System;

namespace Yafc.Core;

public static class IntegerMath {
    public static int CeilingDivide(int dividend, int divisor) {
        ArgumentOutOfRangeException.ThrowIfNegative(dividend);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(divisor);

        int quotient = Math.DivRem(dividend, divisor, out int remainder);
        return remainder == 0 ? quotient : quotient + 1;
    }
}
