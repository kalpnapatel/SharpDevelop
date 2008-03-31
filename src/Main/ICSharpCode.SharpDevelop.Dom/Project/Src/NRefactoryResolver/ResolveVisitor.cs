// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.Ast;
using ICSharpCode.NRefactory.Visitors;

namespace ICSharpCode.SharpDevelop.Dom.NRefactoryResolver
{
	/// <summary>
	/// Resolves expressions.
	/// </summary>
	sealed class ResolveVisitor : AbstractAstVisitor
	{
		NRefactoryResolver resolver;
		
		public ResolveVisitor(NRefactoryResolver resolver)
		{
			this.resolver = resolver;
		}
		
		ResolveResult CreateResolveResult(TypeReference reference)
		{
			return CreateResolveResult(TypeVisitor.CreateReturnType(reference, resolver));
		}
		
		ResolveResult CreateResolveResult(Expression expression)
		{
			return CreateResolveResult(ResolveType(expression));
		}
		
		ResolveResult CreateResolveResult(IReturnType resolvedType)
		{
			if (resolvedType == null)
				return null;
			else
				return new ResolveResult(resolver.CallingClass, resolver.CallingMember, resolvedType);
		}
		
		TypeResolveResult CreateTypeResolveResult(IReturnType resolvedType)
		{
			if (resolvedType == null)
				return null;
			else
				return new TypeResolveResult(resolver.CallingClass, resolver.CallingMember, resolvedType);
		}
		
		MemberResolveResult CreateMemberResolveResult(IMember member)
		{
			if (member == null)
				return null;
			else
				return new MemberResolveResult(resolver.CallingClass, resolver.CallingMember, member);
		}
		
		readonly Dictionary<Expression, ResolveResult> cachedResults = new Dictionary<Expression, ResolveResult>();
		
		public ResolveResult Resolve(Expression expression)
		{
			ResolveResult rr;
			if (!cachedResults.TryGetValue(expression, out rr)) {
				rr = (ResolveResult)expression.AcceptVisitor(this, null);
				if (rr != null)
					rr.Freeze();
				cachedResults[expression] = rr;
			}
			return rr;
		}
		
		public IReturnType ResolveType(Expression expression)
		{
			ResolveResult rr = Resolve(expression);
			if (rr != null)
				return rr.ResolvedType;
			else
				return null;
		}
		
		public override object VisitAnonymousMethodExpression(AnonymousMethodExpression anonymousMethodExpression, object data)
		{
			return CreateResolveResult(new LambdaReturnType(anonymousMethodExpression, resolver));
		}
		
		public override object VisitArrayCreateExpression(ArrayCreateExpression arrayCreateExpression, object data)
		{
			if (arrayCreateExpression.IsImplicitlyTyped) {
				return CreateResolveResult(arrayCreateExpression.ArrayInitializer);
			} else {
				return CreateResolveResult(arrayCreateExpression.CreateType);
			}
		}
		
		public override object VisitAssignmentExpression(AssignmentExpression assignmentExpression, object data)
		{
			return CreateResolveResult(assignmentExpression.Left);
		}
		
		public override object VisitBaseReferenceExpression(BaseReferenceExpression baseReferenceExpression, object data)
		{
			if (resolver.CallingClass == null) {
				return null;
			}
			if (resolver.Language == SupportedLanguage.VBNet && IsInstanceConstructor(resolver.CallingMember)) {
				return new VBBaseOrThisReferenceInConstructorResolveResult(
					resolver.CallingClass, resolver.CallingMember, resolver.CallingClass.BaseType);
			} else {
				return new BaseResolveResult(resolver.CallingClass, resolver.CallingMember, resolver.CallingClass.BaseType);
			}
		}
		
		public override object VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression, object data)
		{
			switch (binaryOperatorExpression.Op) {
				case BinaryOperatorType.NullCoalescing:
					return CreateResolveResult(binaryOperatorExpression.Right);
				case BinaryOperatorType.DivideInteger:
					return CreateResolveResult(resolver.ProjectContent.SystemTypes.Int32);
				case BinaryOperatorType.Concat:
					return CreateResolveResult(resolver.ProjectContent.SystemTypes.String);
				case BinaryOperatorType.Equality:
				case BinaryOperatorType.InEquality:
				case BinaryOperatorType.ReferenceEquality:
				case BinaryOperatorType.ReferenceInequality:
				case BinaryOperatorType.LogicalAnd:
				case BinaryOperatorType.LogicalOr:
				case BinaryOperatorType.LessThan:
				case BinaryOperatorType.LessThanOrEqual:
				case BinaryOperatorType.GreaterThan:
				case BinaryOperatorType.GreaterThanOrEqual:
					return CreateResolveResult(resolver.ProjectContent.SystemTypes.Boolean);
				default:
					return CreateResolveResult(MemberLookupHelper.GetCommonType(
						resolver.ProjectContent,
						ResolveType(binaryOperatorExpression.Left),
						ResolveType(binaryOperatorExpression.Right)));
			}
		}
		
		public override object VisitCastExpression(CastExpression castExpression, object data)
		{
			return CreateResolveResult(castExpression.CastTo);
		}
		
		public override object VisitCheckedExpression(CheckedExpression checkedExpression, object data)
		{
			return CreateResolveResult(checkedExpression.Expression);
		}
		
		public override object VisitClassReferenceExpression(ClassReferenceExpression classReferenceExpression, object data)
		{
			if (resolver.CallingClass != null)
				return CreateResolveResult(resolver.CallingClass.DefaultReturnType);
			else
				return null;
		}
		
		public override object VisitCollectionInitializerExpression(CollectionInitializerExpression collectionInitializerExpression, object data)
		{
			// used for implicitly typed arrays
			if (collectionInitializerExpression.CreateExpressions.Count == 0)
				return null;
			IReturnType combinedRT = ResolveType(collectionInitializerExpression.CreateExpressions[0]);
			for (int i = 1; i < collectionInitializerExpression.CreateExpressions.Count; i++) {
				IReturnType rt = ResolveType(collectionInitializerExpression.CreateExpressions[i]);
				combinedRT = MemberLookupHelper.GetCommonType(resolver.ProjectContent, combinedRT, rt);
			}
			return CreateResolveResult(new ArrayReturnType(resolver.ProjectContent, combinedRT, 1));
		}
		
		public override object VisitConditionalExpression(ConditionalExpression conditionalExpression, object data)
		{
			return CreateResolveResult(MemberLookupHelper.GetCommonType(
				resolver.ProjectContent,
				ResolveType(conditionalExpression.TrueExpression),
				ResolveType(conditionalExpression.FalseExpression)));
		}
		
		public override object VisitConstructorInitializer(ConstructorInitializer constructorInitializer, object data)
		{
			return null;
		}
		
		public override object VisitDefaultValueExpression(DefaultValueExpression defaultValueExpression, object data)
		{
			return CreateResolveResult(defaultValueExpression.TypeReference);
		}
		
		public override object VisitDirectionExpression(DirectionExpression directionExpression, object data)
		{
			return CreateResolveResult(new ReferenceReturnType(ResolveType(directionExpression.Expression)));
		}
		
		public override object VisitIdentifierExpression(IdentifierExpression identifierExpression, object data)
		{
			return resolver.ResolveIdentifier(identifierExpression, ExpressionContext.Default);
		}
		
		public override object VisitIndexerExpression(IndexerExpression indexerExpression, object data)
		{
			IReturnType target = ResolveType(indexerExpression.TargetObject);
			return CreateMemberResolveResult(
				GetIndexer(target, indexerExpression.Indexes)
			);
		}
		
		IProperty GetIndexer(IReturnType target, List<Expression> indexes)
		{
			if (target == null)
				return null;
			return MemberLookupHelper.FindOverload(
				target.GetProperties().Where((IProperty p) => p.IsIndexer).ToList(),
				indexes.Select<Expression, IReturnType>(ResolveType).ToArray()
			);
		}
		
		IProperty GetVisualBasicIndexer(InvocationExpression invocationExpression)
		{
			ResolveResult targetRR = Resolve(invocationExpression.TargetObject);
			if (targetRR != null) {
				// Visual Basic can call indexers in two ways:
				// collection(index) - use indexer
				// collection.Item(index) - use parametrized property
				
				if (invocationExpression.TargetObject is IdentifierExpression || invocationExpression.TargetObject is MemberReferenceExpression) {
					// only IdentifierExpression/MemberReferenceExpression can represent a parametrized property
					// - the check is necessary because collection.Items and collection.Item(index) both
					// resolve to the same property, but we want to use the default indexer for the second call in
					// collection.Item(index1)(index2)
					MemberResolveResult memberRR = targetRR as MemberResolveResult;
					if (memberRR != null)  {
						IProperty p = memberRR.ResolvedMember as IProperty;
						if (p != null && p.Parameters.Count > 0) {
							// this is a parametrized property
							return p;
						}
					}
				}
				// not a parametrized property - try normal indexer
				return GetIndexer(targetRR.ResolvedType, invocationExpression.Arguments);
			} else {
				return null;
			}
		}
		
		public override object VisitInvocationExpression(InvocationExpression invocationExpression, object data)
		{
			if (resolver.Language == SupportedLanguage.CSharp && resolver.CallingClass != null) {
				if (invocationExpression.TargetObject is ThisReferenceExpression) {
					// call to constructor
					return ResolveConstructorOverload(resolver.CallingClass, invocationExpression.Arguments);
				} else if (invocationExpression.TargetObject is BaseReferenceExpression) {
					return ResolveConstructorOverload(resolver.CallingClass.BaseClass, invocationExpression.Arguments);
				}
			}
			
			ResolveResult rr = Resolve(invocationExpression.TargetObject);
			MixedResolveResult mixedRR = rr as MixedResolveResult;
			if (mixedRR != null) {
				rr = mixedRR.PrimaryResult;
			}
			MethodGroupResolveResult mgrr = rr as MethodGroupResolveResult;
			if (mgrr != null) {
				if (resolver.Language == SupportedLanguage.VBNet && mgrr.Methods.All(mg => mg.Count == 0))
					return CreateMemberResolveResult(GetVisualBasicIndexer(invocationExpression));
				
				IReturnType[] argumentTypes = invocationExpression.Arguments.Select<Expression, IReturnType>(ResolveType).ToArray();
				
				MemberResolveResult firstResult = null;
				foreach (MethodGroup methodGroup in mgrr.Methods) {
					bool resultIsAcceptable;
					IMethod method;
					if (methodGroup.IsExtensionMethodGroup) {
						IReturnType[] extendedTypes = new IReturnType[argumentTypes.Length + 1];
						extendedTypes[0] = mgrr.ContainingType;
						argumentTypes.CopyTo(extendedTypes, 1);
						method = MemberLookupHelper.FindOverload(methodGroup, extendedTypes, out resultIsAcceptable);
					} else {
						method = MemberLookupHelper.FindOverload(methodGroup, argumentTypes, out resultIsAcceptable);
					}
					MemberResolveResult result = CreateMemberResolveResult(method);
					if (result != null && methodGroup.IsExtensionMethodGroup)
						result.IsExtensionMethodCall = true;
					if (resultIsAcceptable)
						return result;
					if (firstResult == null)
						firstResult = result;
				}
				if (firstResult != null) {
					return firstResult;
				} else {
					return FallbackResolveMethod(invocationExpression, mgrr, argumentTypes);
				}
			} else if (rr != null && rr.ResolvedType != null) {
				IClass c = rr.ResolvedType.GetUnderlyingClass();
				if (c != null && c.ClassType == ClassType.Delegate) {
					// We don't want to show "System.EventHandler.Invoke" in the tooltip
					// of "EventCall(this, EventArgs.Empty)", we just show the event/delegate for now
					// but for DelegateCall(params).* completion, we use the delegate's
					// return type instead of the delegate type itself
					
					IMethod method = rr.ResolvedType.GetMethods().FirstOrDefault(innerMethod => innerMethod.Name == "Invoke");
					if (method != null) {
						return new DelegateCallResolveResult(rr, method);
					}
				}
			}
			if (resolver.Language == SupportedLanguage.VBNet) {
				return CreateMemberResolveResult(GetVisualBasicIndexer(invocationExpression));
			}
			return null;
		}
		
		ResolveResult FallbackResolveMethod(InvocationExpression invocation, MethodGroupResolveResult mgrr, IReturnType[] argumentTypes)
		{
			// method not found, let's try if we can find a method if we violate the
			// accessibility rules
			MemberReferenceExpression mre = invocation.TargetObject as MemberReferenceExpression;
			if (mre != null) {
				List<IMethod> methods = mgrr.ContainingType.GetMethods().Where(m => resolver.IsSameName(m.Name, mre.MemberName)).ToList();
				bool resultIsAcceptable;
				IMethod result = MemberLookupHelper.FindOverload(
					methods, argumentTypes, out resultIsAcceptable);
				if (result != null) {
					return CreateMemberResolveResult(result);
				}
			}
			
			// TODO: method still not found, now return invalid ResolveResult describing the expected method
			return null;
		}
		
		public override object VisitLambdaExpression(LambdaExpression lambdaExpression, object data)
		{
			return CreateResolveResult(new LambdaReturnType(lambdaExpression, resolver));
		}
		
		public override object VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression, object data)
		{
			IReturnType type;
			if (string.IsNullOrEmpty(memberReferenceExpression.MemberName)) {
				// NRefactory creates this "dummy" fieldReferenceExpression when it should
				// parse a primitive type name (int, short; Integer, Decimal)
				if (memberReferenceExpression.TargetObject is TypeReferenceExpression) {
					type = TypeVisitor.CreateReturnType(((TypeReferenceExpression)memberReferenceExpression.TargetObject).TypeReference, resolver);
					return CreateTypeResolveResult(type);
				}
			}
			ResolveResult targetRR = Resolve(memberReferenceExpression.TargetObject);
			if (targetRR == null)
				return null;
			type = targetRR.ResolvedType;
			if (targetRR is NamespaceResolveResult) {
				return ResolveMemberInNamespace(((NamespaceResolveResult)targetRR).Name, memberReferenceExpression);
			} else if (type != null) {
				TypeResolveResult typeRR = targetRR as TypeResolveResult;
				if (typeRR != null && typeRR.ResolvedClass != null) {
					foreach (IClass c1 in typeRR.ResolvedClass.ClassInheritanceTree) {
						foreach (IClass c in c1.InnerClasses) {
							if (resolver.IsSameName(memberReferenceExpression.MemberName, c.Name)
							    && c.TypeParameters.Count == memberReferenceExpression.TypeArguments.Count)
							{
								return CreateTypeResolveResult(resolver.ConstructType(c.DefaultReturnType, memberReferenceExpression.TypeArguments));
							}
						}
					}
				}
				return resolver.ResolveMember(type, memberReferenceExpression.MemberName,
				                              memberReferenceExpression.TypeArguments,
				                              NRefactoryResolver.IsInvoked(memberReferenceExpression),
				                              typeRR == null, // allow extension methods only for non-static method calls
				                              targetRR is BaseResolveResult ? (bool?)true : null // allow calling protected members using "base."
				                             );
			}
			return null;
		}
		
		ResolveResult ResolveMemberInNamespace(string namespaceName, MemberReferenceExpression mre)
		{
			string combinedName;
			if (string.IsNullOrEmpty(namespaceName))
				combinedName = mre.MemberName;
			else
				combinedName = namespaceName + "." + mre.MemberName;
			if (resolver.ProjectContent.NamespaceExists(combinedName)) {
				return new NamespaceResolveResult(resolver.CallingClass, resolver.CallingMember, combinedName);
			}
			IClass c = resolver.GetClass(combinedName, mre.TypeArguments.Count);
			if (c != null) {
				return CreateTypeResolveResult(resolver.ConstructType(c.DefaultReturnType, mre.TypeArguments));
			}
			if (resolver.LanguageProperties.ImportModules) {
				// go through the members of the modules
				List<IMember> possibleMembers = new List<IMember>();
				foreach (object o in resolver.ProjectContent.GetNamespaceContents(namespaceName)) {
					IMember member = o as IMember;
					if (member != null && resolver.IsSameName(member.Name, mre.MemberName)) {
						possibleMembers.Add(member);
					}
				}
				return resolver.CreateMemberOrMethodGroupResolveResult(
					null, mre.MemberName, new IList<IMember>[] { possibleMembers }, false);
			}
			return null;
		}
		
		public override object VisitObjectCreateExpression(ObjectCreateExpression objectCreateExpression, object data)
		{
			if (objectCreateExpression.IsAnonymousType) {
				return CreateResolveResult(CreateAnonymousTypeClass(objectCreateExpression.ObjectInitializer).DefaultReturnType);
			} else {
				IReturnType rt = TypeVisitor.CreateReturnType(objectCreateExpression.CreateType, resolver);
				if (rt == null)
					return new UnknownConstructorCallResolveResult(resolver.CallingClass, resolver.CallingMember, objectCreateExpression.CreateType.ToString());
				
				return ResolveConstructorOverload(rt, objectCreateExpression.Parameters)
					?? CreateResolveResult(rt);
			}
		}
		
		internal ResolveResult ResolveConstructorOverload(IReturnType rt, List<Expression> arguments)
		{
			if (rt == null)
				return null;
			ResolveResult rr = ResolveConstructorOverload(rt.GetUnderlyingClass(), arguments);
			if (rr != null)
				rr.ResolvedType = rt;
			return rr;
		}
		
		internal ResolveResult ResolveConstructorOverload(IClass c, List<Expression> arguments)
		{
			if (c == null)
				return null;

			List<IMethod> methods = c.Methods.Where(m => m.IsConstructor && !m.IsStatic).ToList();
			IReturnType[] argumentTypes = arguments.Select<Expression, IReturnType>(ResolveType).ToArray();
			bool resultIsAcceptable;
			IMethod result = MemberLookupHelper.FindOverload(methods, argumentTypes, out resultIsAcceptable);
			return CreateMemberResolveResult(result ?? Constructor.CreateDefault(c));
		}
		
		DefaultClass CreateAnonymousTypeClass(CollectionInitializerExpression initializer)
		{
			List<IReturnType> fieldTypes = new List<IReturnType>();
			List<string> fieldNames = new List<string>();
			
			foreach (Expression expr in initializer.CreateExpressions) {
				if (expr is AssignmentExpression) {
					// use right part only
					fieldTypes.Add( ResolveType(((AssignmentExpression)expr).Right) );
				} else {
					fieldTypes.Add( ResolveType(expr) );
				}
				
				fieldNames.Add(GetAnonymousTypeFieldName(expr));
			}
			
			StringBuilder nameBuilder = new StringBuilder();
			nameBuilder.Append('{');
			for (int i = 0; i < fieldTypes.Count; i++) {
				if (i > 0) nameBuilder.Append(", ");
				nameBuilder.Append(fieldNames[i]);
				nameBuilder.Append(" : ");
				if (fieldTypes[i] != null) {
					nameBuilder.Append(fieldTypes[i].DotNetName);
				}
			}
			nameBuilder.Append('}');
			
			DefaultClass c = new DefaultClass(new DefaultCompilationUnit(resolver.ProjectContent), nameBuilder.ToString());
			c.Modifiers = ModifierEnum.Internal | ModifierEnum.Synthetic | ModifierEnum.Sealed;
			for (int i = 0; i < fieldTypes.Count; i++) {
				DefaultProperty p = new DefaultProperty(fieldNames[i], fieldTypes[i], ModifierEnum.Public | ModifierEnum.Synthetic, DomRegion.Empty, DomRegion.Empty, c);
				p.CanGet = true;
				p.CanSet = false;
				c.Properties.Add(p);
			}
			return c;
		}
		
		static string GetAnonymousTypeFieldName(Expression expr)
		{
			if (expr is MemberReferenceExpression) {
				return ((MemberReferenceExpression)expr).MemberName;
			}
			if (expr is AssignmentExpression) {
				expr = ((AssignmentExpression)expr).Left; // use left side if it is an IdentifierExpression
			}
			if (expr is IdentifierExpression) {
				return ((IdentifierExpression)expr).Identifier;
			} else {
				return "?";
			}
		}
		
		public override object VisitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression, object data)
		{
			return CreateResolveResult(parenthesizedExpression.Expression);
		}
		
		public override object VisitPointerReferenceExpression(PointerReferenceExpression pointerReferenceExpression, object data)
		{
			return null;
		}
		
		public override object VisitPrimitiveExpression(PrimitiveExpression primitiveExpression, object data)
		{
			if (primitiveExpression.Value == null) {
				return CreateResolveResult(NullReturnType.Instance);
			} else if (primitiveExpression.Value is int) {
				return new IntegerLiteralResolveResult(resolver.CallingClass, resolver.CallingMember, resolver.ProjectContent.SystemTypes.Int32);
			} else {
				return CreateResolveResult(resolver.ProjectContent.SystemTypes.CreatePrimitive(primitiveExpression.Value.GetType()));
			}
		}
		
		public override object VisitQueryExpression(QueryExpression queryExpression, object data)
		{
			IReturnType type = null;
			QueryExpressionSelectClause selectClause = queryExpression.SelectOrGroupClause as QueryExpressionSelectClause;
			QueryExpressionGroupClause groupClause = queryExpression.SelectOrGroupClause as QueryExpressionGroupClause;
			if (selectClause != null) {
				type = ResolveType(selectClause.Projection);
			} else if (groupClause != null) {
				type = new ConstructedReturnType(
					new GetClassReturnType(resolver.ProjectContent, "System.Linq.IGrouping", 2),
					new IReturnType[] { ResolveType(groupClause.GroupBy), ResolveType(groupClause.Projection) }
				);
			}
			if (type != null) {
				return CreateResolveResult(new ConstructedReturnType(
					new GetClassReturnType(resolver.ProjectContent, "System.Collections.Generic.IEnumerable", 1),
					new IReturnType[] { type }
				));
			} else {
				return null;
			}
		}
		
		public override object VisitSizeOfExpression(SizeOfExpression sizeOfExpression, object data)
		{
			return CreateResolveResult(resolver.ProjectContent.SystemTypes.Int32);
		}
		
		public override object VisitStackAllocExpression(StackAllocExpression stackAllocExpression, object data)
		{
			return null;
		}
		
		public override object VisitThisReferenceExpression(ThisReferenceExpression thisReferenceExpression, object data)
		{
			if (resolver.CallingClass == null)
				return null;
			if (resolver.Language == SupportedLanguage.VBNet && IsInstanceConstructor(resolver.CallingMember)) {
				return new VBBaseOrThisReferenceInConstructorResolveResult(
					resolver.CallingClass, resolver.CallingMember, resolver.CallingClass.DefaultReturnType);
			} else {
				return CreateResolveResult(resolver.CallingClass.DefaultReturnType);
			}
		}
		
		static bool IsInstanceConstructor(IMember member)
		{
			IMethod m = member as IMethod;
			return m != null && m.IsConstructor && !m.IsStatic;
		}
		
		public override object VisitTypeOfExpression(TypeOfExpression typeOfExpression, object data)
		{
			return CreateResolveResult(resolver.ProjectContent.SystemTypes.Type);
		}
		
		public override object VisitTypeOfIsExpression(TypeOfIsExpression typeOfIsExpression, object data)
		{
			return CreateResolveResult(resolver.ProjectContent.SystemTypes.Boolean);
		}
		
		public override object VisitTypeReferenceExpression(TypeReferenceExpression typeReferenceExpression, object data)
		{
			TypeReference reference = typeReferenceExpression.TypeReference;
			ResolveResult rr = CreateResolveResult(reference);
			if (rr == null && reference.GenericTypes.Count == 0 && !reference.IsArrayType) {
				// reference to namespace is possible
				if (reference.IsGlobal) {
					if (resolver.ProjectContent.NamespaceExists(reference.Type))
						return new NamespaceResolveResult(resolver.CallingClass, resolver.CallingMember, reference.Type);
				} else {
					string name = resolver.SearchNamespace(reference.Type, typeReferenceExpression.StartLocation);
					if (name != null)
						return new NamespaceResolveResult(resolver.CallingClass, resolver.CallingMember, name);
				}
			}
			return rr;
		}
		
		public override object VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression, object data)
		{
			return CreateResolveResult(unaryOperatorExpression.Expression);
		}
		
		public override object VisitUncheckedExpression(UncheckedExpression uncheckedExpression, object data)
		{
			return CreateResolveResult(uncheckedExpression.Expression);
		}
	}
}
