using System.Linq.Expressions;
using System.Reflection;
using BLRefactoring.Shared.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BLRefactoring.DDD.Infrastructure.Repositories.EfCore.Interceptor;

public class IsTransientMaterializationInterceptor : IMaterializationInterceptor
{
    private static readonly Dictionary<Type, Action<object, bool>> IsTransientSetters = new();
    private static readonly Dictionary<Type, FieldInfo> IsTransientFieldInfos = new();

    private static readonly HashSet<Type> EntityTypes = new();

    public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
    {
        var entityType = materializationData.EntityType.ConstructorBinding!.RuntimeType;
        if (!IsAssignableToGenericType(entityType, typeof(Entity<>)))
        {
            return instance;
        }

        if (!IsTransientSetters.TryGetValue(entityType, out var isTransientSetter))
        {
            isTransientSetter = BuildSetIsTransientDelegate(entityType);
            IsTransientSetters[entityType] = isTransientSetter;
        }

        isTransientSetter(instance, false);

        return instance;
    }

    private static Action<object, bool> BuildSetIsTransientDelegate(Type entityType)
    {
        var fieldInfo = GetFieldInBaseClass(entityType, "_isTransient");
        if (fieldInfo == null)
        {
            throw new InvalidOperationException($"Field '_isTransient' not found in type {entityType}.");
        }

        var inputObject = Expression.Parameter(typeof(object));
        var valueToSet = Expression.Parameter(typeof(bool));

        var typedInputObject = Expression.Convert(inputObject, entityType);
        var fieldInformation = Expression.Field(typedInputObject, fieldInfo);
        var fieldAssignment = Expression.Assign(fieldInformation, valueToSet);

        return Expression.Lambda<Action<object, bool>>(fieldAssignment, inputObject, valueToSet).Compile();
    }

    private static bool IsAssignableToGenericType(Type givenType, Type genericType)
    {
        if (EntityTypes.Contains(givenType) ||
            (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType))
        {
            return true;
        }

        var baseType = givenType.BaseType;

        while (baseType is not null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == genericType)
            {
                EntityTypes.Add(givenType);
                return true;
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    private static FieldInfo? GetFieldInBaseClass(Type type, string fieldName)
    {
        var originalType = type;

        if (IsTransientFieldInfos.TryGetValue(originalType, out var fieldInfo))
        {
            return fieldInfo;
        }

        while (type != null && type != typeof(object))
        {
            fieldInfo = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                IsTransientFieldInfos[originalType] = fieldInfo;
                return fieldInfo;
            }

            type = type.BaseType;
        }

        return null;
    }
}
