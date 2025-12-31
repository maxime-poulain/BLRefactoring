using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using BLRefactoring.Shared.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptor;

/// <summary>
/// Interceptor for handling the "IsTransient" property of entities during the SaveChanges process in Entity Framework.
/// </summary>
public class IsTransientSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// A thread-safe dictionary that caches delegates for setting the "IsTransient" property of entities.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Action<object, bool>> IsTransientSetters = new();

    /// <summary>
    /// A thread-safe dictionary that caches field information for the "IsTransient" property of entities.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, FieldInfo> IsTransientFieldInfos = new();

    /// <summary>
    /// A set of entity types that are assignable to the generic type <see cref="Entity{T}"/>.
    /// </summary>
    private static readonly HashSet<Type> EntityTypes = [];

    /// <summary>
    /// Overrides the SavedChangesAsync method to reset the "IsTransient" property of entities after changes are saved.
    /// </summary>
    /// <param name="eventData">The event data for the save changes operation.</param>
    /// <param name="result">The result of the save changes operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of the save changes operation.</returns>
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context ?? throw new InvalidOperationException("DbContext is null in SaveChangesCompletedEventData.");
        foreach (var entry in context.ChangeTracker.Entries())
        {
            var entityType = entry.Entity.GetType();
            if (!IsAssignableToGenericType(entityType, typeof(Entity<>)))
            {
                continue;
            }

            if (!IsTransientSetters.TryGetValue(entityType, out var isTransientSetter))
            {
                isTransientSetter = BuildSetIsTransientDelegate(entityType);
                IsTransientSetters[entityType] = isTransientSetter;
            }

            isTransientSetter(entry.Entity, false);
        }
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Builds a delegate for setting the "IsTransient" property of an entity.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <returns>A delegate that sets the "IsTransient" property of the entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the "_isTransient" field is not found in the entity type.</exception>
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

    /// <summary>
    /// Determines whether a given type is assignable to a specified generic type.
    /// </summary>
    /// <param name="givenType">The type to check.</param>
    /// <param name="genericType">The generic type to check against.</param>
    /// <returns>True if the given type is assignable to the generic type; otherwise, false.</returns>
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

    /// <summary>
    /// Retrieves the field information for a specified field in the base class of a given type.
    /// </summary>
    /// <param name="type">The type to search.</param>
    /// <param name="fieldName">The name of the field to find.</param>
    /// <returns>The field information if found; otherwise, null.</returns>
    private static FieldInfo? GetFieldInBaseClass(Type type, string fieldName)
    {
        Type originalType = type;

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
