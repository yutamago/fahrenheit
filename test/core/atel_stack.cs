// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

using Fahrenheit.Atel;

namespace Fahrenheit.Tests;

[TestFixture]
public class FhAtelStackTests {

    [Test]
    public void atel_stack_sizeof() {
        Assert.That(Unsafe.SizeOf<AtelStack>(), Is.EqualTo(0x68)); // +0 depth, +4 values, +0x54 types
    }

    [Test]
    public void atel_stack_pop_converts_like_the_game() {
        AtelStack stack = new();

        stack.push_float(2.75f);
        stack.push_int(-3);
        stack.push_float(-2.75f);
        stack.push_float(float.NaN);
        stack.push_float(3e9f);

        using (Assert.EnterMultipleScope()) {
            Assert.That(stack.pop_int(),   Is.EqualTo(int.MinValue)); // out of range
            Assert.That(stack.pop_int(),   Is.EqualTo(int.MinValue)); // NaN
            Assert.That(stack.pop_int(),   Is.EqualTo(-2));           // truncated toward zero
            Assert.That(stack.pop_float(), Is.EqualTo(-3f));          // int converted
            Assert.That(stack.pop_float(), Is.EqualTo(2.75f));        // float kept, not truncated
        }
    }

    [Test]
    public void atel_stack_holds_19_values() {
        AtelStack stack = new();
        for (int i = 0; i < 19; i++) stack.push_int(i);

        Assert.Throws<AtelStackOverflowException>(() => stack.push_int(19));
    }
}
