/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Linq;

namespace chibias.Internal;

partial class Parser
{
    private void ParseEnumerationMember(Token[] tokens)
    {
        if (tokens.Length < 1)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing member declaration.");
        }
        else if (tokens.Length > 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
        }
        else
        {
            var fieldAttributes =
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal;

            var memberNameToken = tokens[0];
            var memberName = memberNameToken.Text;

            object memberValue;

            var memberValueToken = tokens.ElementAtOrDefault(1);

            if (memberValueToken is { })
            {
                if (!this.enumerationManipulator!.TryParseMemberValue(memberValueToken, out memberValue))
                {
                    this.OutputError(
                        memberValueToken,
                        $"Invalid member value: {memberValueToken.Text}");
                    memberValue = this.enumerationManipulator!.GetInitialMemberValue();
                }
            }
            else if (this.enumerationType!.Fields.
                LastOrDefault(f => f.IsPublic && f.IsStatic && f.IsLiteral) is { } lastField)
            {
                memberValue = this.enumerationManipulator!.IncrementMemberValue(lastField.Constant);
            }
            else
            {
                memberValue = this.enumerationManipulator!.GetInitialMemberValue();
            }

            // Checks existing structure member declaration.
            if (this.checkingMemberIndex >= 0)
            {
                var field = this.enumerationType!.Fields.
                    Where(f => f.IsPublic && f.IsStatic && f.IsLiteral).
                    ElementAtOrDefault(this.checkingMemberIndex);

                if (field == null)
                {
                    this.OutputError(
                        memberNameToken,
                        $"Enumeration member difference exists before declared type: {memberName}");
                }
                else if (field.Name != memberName)
                {
                    this.OutputError(
                        memberNameToken,
                        $"Enumeration member name difference exists before declared type: {field.Name}");
                }
                else if ((field.Attributes & fieldAttributes) != fieldAttributes)
                {
                    this.OutputError(
                        memberNameToken,
                        $"Enumeration member attributes difference exists before declared type: {field.Attributes}");
                }
                else if (field.FieldType.FullName != this.enumerationUnderlyingType!.FullName)
                {
                    this.OutputError(
                        memberNameToken,
                        $"Enumeration member underlying type difference exists before declared type: {field.FieldType.FullName}");
                }
                else if (field.Constant?.Equals(memberValue) ?? false)
                {
                    this.OutputError(
                        memberNameToken,
                        $"Enumeration member value difference exists before declared type: {field.Constant ?? "(null)"}");
                }

                this.checkingMemberIndex++;
            }
            // Create a field into this enumeration.
            else
            {
                var field = new FieldDefinition(
                    memberName,
                    fieldAttributes,
                    this.enumerationUnderlyingType!);
                field.Constant = memberValue;

                this.enumerationType!.Fields.Add(field);
            }
        }
    }
}
