﻿using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Identity;
using ReactAdvantage.Api.GraphQLSchema.Models;
using ReactAdvantage.Api.GraphQLSchema.Types;
using ReactAdvantage.Data;
using ReactAdvantage.Domain.Configuration;
using ReactAdvantage.Domain.Models;

namespace ReactAdvantage.Api.GraphQLSchema
{
    public class ReactAdvantageMutation : ObjectGraphType
    {
        public ReactAdvantageMutation(
            ReactAdvantageContext db,
            UserManager<User> userManager
            )
        {
            Field<TenantType>(
                "addTenant",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<TenantInputType>> { Name = "tenant" }
                ),
                resolve: context =>
                {
                    context.GetUserContext().EnsureIsInRole(RoleNames.HostAdministrator);

                    var tenant = context.GetArgument<Tenant>("tenant");
                    tenant.Id = 0;
                    db.Add(tenant);
                    db.SaveChanges();
                    return tenant;
                });

            Field<TenantType>(
                "editTenant",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<TenantInputType>> { Name = "tenant" }
                ),
                resolve: context =>
                {
                    context.GetUserContext().EnsureIsInRole(RoleNames.HostAdministrator);

                    var tenant = context.GetArgument<Tenant>("tenant");
                    var entity = db.Tenants.Find(tenant.Id);
                    db.Entry(entity).CurrentValues.SetValues(tenant);
                    db.SaveChanges();
                    return tenant;
                });

            Field<UserType>(
                "addUser",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<UserInputType>> { Name = "user" }
                ),
                resolve: context =>
                {
                    context.GetUserContext().EnsureIsInRole(RoleNames.HostAdministrator);

                    var userInput = context.GetArgument<UserInput>("user");
                    userInput.Id = null;
                    if (string.IsNullOrEmpty(userInput.Password))
                    {
                        userManager.CreateAsync(userInput).GetAwaiter().GetResult().ThrowOnError();
                    }
                    else
                    {
                        userManager.CreateAsync(userInput, userInput.Password).GetAwaiter().GetResult().ThrowOnError();
                    }
                    return userInput;
                });

            Field<UserType>(
                "editUser",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<UserInputType>> { Name = "user" }
                ),
                resolve: context =>
                {
                    var userInput = context.GetArgument<UserInput>("user");

                    var userContext = context.GetUserContext();
                    var isHostAdmin = userContext.IsInRole(RoleNames.HostAdministrator);
                    var isEditingSelf = userContext.Id == userInput.Id;
                    if (!isHostAdmin && !isEditingSelf)
                    {
                        throw new ExecutionError($"Unauthorized. You have to be a member of {RoleNames.HostAdministrator}" 
                                                 + " role to be able to edit any user, otherwise you can only edit" 
                                                 + $" your own user (id: {userContext.Id}).");
                    }
                    
                    var user = userManager.FindByIdAsync(userInput.Id).GetAwaiter().GetResult();

                    if (!string.IsNullOrEmpty(userInput.Password))
                    {
                        //validate the password before editing the user so we can leave the user unchanged if the validation fails
                        foreach (var validator in userManager.PasswordValidators)
                        {
                            validator.ValidateAsync(userManager, user, userInput.Password).GetAwaiter().GetResult()
                                .ThrowOnError();
                        }
                    }

                    user.UpdateValuesFrom(userInput);
                    userManager.UpdateAsync(user).GetAwaiter().GetResult().ThrowOnError();

                    if (!string.IsNullOrEmpty(userInput.Password))
                    {
                        userManager.RemovePasswordAsync(user).GetAwaiter().GetResult().ThrowOnError();
                        userManager.AddPasswordAsync(user, userInput.Password).GetAwaiter().GetResult().ThrowOnError();
                        //the below doesn't work without adding IUserTwoFactorTokenProvider
                        //var resetToken = userManager.GeneratePasswordResetTokenAsync(user).GetAwaiter().GetResult();
                        //userManager.ResetPasswordAsync(user, resetToken, userInput.Password).GetAwaiter().GetResult().ThrowOnError();
                    }

                    return userInput;
                });

            Field<ProjectType>(
                "addProject",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ProjectInputType>> { Name = "project" }
                ),
                resolve: context =>
                {
                    var project = context.GetArgument<Project>("project");
                    project.Id = 0;
                    project.SetTenantIdOrThrow(context);
                    db.Add(project);
                    db.SaveChanges();
                    return project;
                });

            Field<ProjectType>(
                "editProject",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ProjectInputType>> { Name = "project" }
                ),
                resolve: context =>
                {
                    var project = context.GetArgument<Project>("project");
                    var entity = db.Projects.Find(project.Id);
                    entity.UpdateValuesFrom(project);
                    db.SaveChanges();
                    return project;
                });

            Field<TaskType>(
                "addTask",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<TaskInputType>> { Name = "task" }
                ),
                resolve: context =>
                {
                    var task = context.GetArgument<Task>("task");
                    task.Id = 0;
                    task.SetTenantIdOrThrow(context);
                    db.Add(task);
                    db.SaveChanges();
                    return task;
                });

            Field<TaskType>(
                "editTask",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<TaskInputType>> { Name = "task" }
                ),
                resolve: context =>
                {
                    var task = context.GetArgument<Task>("task");
                    var entity = db.Tasks.Find(task.Id);
                    entity.UpdateValuesFrom(task);
                    db.SaveChanges();
                    return task;
                });
        }
    }
}
