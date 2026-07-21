using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dj.test;
using FluentAssertions;

[assembly: FluentAssertions.Extensibility.AssertionEngineInitializer(
    typeof(AssertionEngineInitializer),
    nameof(AssertionEngineInitializer.AcknowledgeSoftWarning))]

namespace dj.test;

public static class AssertionEngineInitializer
{
    public static void AcknowledgeSoftWarning()
    {
        License.Accepted = true;
    }
}