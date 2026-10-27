// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tests;

[TestFixture]
public class FhUtilTests {

    [Test]
    public void set_bits_writes_the_low_bits_of_value() {
        uint field = 0;
        field.set_bits(7, 2, 3u);

        using (Assert.EnterMultipleScope()) {
            Assert.That(field,                Is.EqualTo(0x180u));
            Assert.That(field.get_bits(7, 2), Is.EqualTo(3u));
        }
    }

    [Test]
    public void set_bits_keeps_the_other_bits() {
        uint field = 0xFFFF_FFFF;
        field.set_bits(9, 2, 1u);

        Assert.That(field, Is.EqualTo(0xFFFF_FBFFu));
    }

    [Test]
    public void set_bits_ignores_bits_above_len() {
        int field = 0;
        field.set_bits(17, 15, -1);

        using (Assert.EnterMultipleScope()) {
            Assert.That(field,                  Is.EqualTo(unchecked((int)0xFFFE_0000)));
            Assert.That(field.get_bits(0, 17), Is.EqualTo(0));
        }
    }

    [Test]
    public void set_bits_round_trips_with_get_bits() {
        ushort field = 0;
        field.set_bits(3, 5, (ushort)0x15);
        field.set_bits(8, 4, (ushort)0x9);

        using (Assert.EnterMultipleScope()) {
            Assert.That(field.get_bits(3, 5), Is.EqualTo((ushort)0x15));
            Assert.That(field.get_bits(8, 4), Is.EqualTo((ushort)0x9));
            Assert.That(field.get_bits(0, 3), Is.EqualTo((ushort)0));
        }
    }

}
