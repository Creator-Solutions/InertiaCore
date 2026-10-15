using InertiaCore.Extensions;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestPrecognitionDetection
{
    private static DefaultHttpContext Context(params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        foreach (var (name, value) in headers)
            context.Request.Headers[name] = value;

        return context;
    }

    [Test]
    [Description("Precognition is detected only for the literal 'true' header value.")]
    public void IsPrecognitionRequest_OnlyLiteralTrue()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Context((InertiaHeader.Precognition, "true")).IsPrecognitionRequest(), Is.True);
            Assert.That(Context((InertiaHeader.Precognition, "false")).IsPrecognitionRequest(), Is.False);
            Assert.That(Context((InertiaHeader.Precognition, "1")).IsPrecognitionRequest(), Is.False);
            Assert.That(Context().IsPrecognitionRequest(), Is.False);
        });
    }

    [Test]
    [Description("Validate-Only is split, trimmed and matched case-insensitively.")]
    public void ValidateOnly_ParsesFields()
    {
        var context = Context((InertiaHeader.PrecognitionValidateOnly, " name , Email ,, "));

        var fields = context.GetPrecognitionValidateOnly();

        Assert.Multiple(() =>
        {
            Assert.That(fields, Has.Count.EqualTo(2));
            Assert.That(fields.Contains("name"), Is.True);
            Assert.That(fields.Contains("EMAIL"), Is.True, "comparison should be case-insensitive");
        });
    }

    [Test]
    [Description("An absent or blank Validate-Only header yields an empty set.")]
    public void ValidateOnly_AbsentOrBlank_IsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Context().GetPrecognitionValidateOnly(), Is.Empty);
            Assert.That(Context((InertiaHeader.PrecognitionValidateOnly, "   ")).GetPrecognitionValidateOnly(), Is.Empty);
        });
    }
}
