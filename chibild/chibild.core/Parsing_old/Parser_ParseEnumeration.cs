/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using Mono.Cecil;
using System.Linq;

namespace chibild.Parsing;

partial class Parser
{
    private void ParseEnumerationMember(Token[] tokens)
    {
        if (tokens.Length < 1)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing member declaration.");
            return;
        }
        
        if (tokens.Length > 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
            return;
        }

        var fieldAttributes =
            FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal;

        var memberNameToken = tokens[0];
        var memberName = memberNameToken.Text;

        object memberValue;
        if (tokens.ElementAtOrDefault(1) is { } memberValueToken)
        {
            if (!this.enumerationManipulator!.TryParseMemberValue(memberValueToken, out memberValue))
            {
                this.OutputError(
                    memberValueToken,
                    $"Invalid member value: {memberValueToken.Text}");
                return;
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
            var ef = this.enumerationType!.Fields.
                Where(f => f.IsPublic && f.IsStatic && f.IsLiteral).
                ElementAtOrDefault(this.checkingMemberIndex);

            this.checkingMemberIndex++;

            if (ef == null)
            {
                this.OutputError(
                    memberNameToken,
                    $"Enumeration member difference exists before declared type: {memberName}");
                return;
            }

            if (ef.Name != memberName)
            {
                this.OutputError(
                    memberNameToken,
                    $"Enumeration member name difference exists before declared type: {ef.Name}");
                return;
            }
            
            if ((ef.Attributes & fieldAttributes) != fieldAttributes)
            {
                this.OutputError(
                    memberNameToken,
                    $"Enumeration member attributes difference exists before declared type: {ef.Attributes}");
                return;
            }

            if (ef.FieldType.FullName != this.enumerationUnderlyingType!.FullName)
            {
                this.OutputError(
                    memberNameToken,
                    $"Enumeration member underlying type difference exists before declared type: {ef.FieldType.FullName}");
                return;
            }
            
            if (!(ef.Constant?.Equals(memberValue) ?? true))
            {
                this.OutputError(
                    memberNameToken,
                    $"Enumeration member value difference exists before declared type: {ef.Constant ?? "(null)"}");
                return;
            }

            return;
        }

        // Create a field into this enumeration.
        var field = new FieldDefinition(
            memberName,
            fieldAttributes,
            this.enumerationUnderlyingType!);
        field.Constant = memberValue;

        this.enumerationType!.Fields.Add(field);
    }
}
