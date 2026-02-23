// Copyright Epic Games, Inc. All Rights Reserved.

#include "Testing/LyraTestEnemyQuery.h"
#include "Testing/LyraTestSupportSubsystem.h"
#include "Character/LyraHealthComponent.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "Engine/GameInstance.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/Character.h"
#include "Kismet/GameplayStatics.h"
#include "Camera/PlayerCameraManager.h"
#include "Math/UnrealMathUtility.h"
#include "Teams/LyraTeamSubsystem.h"

#include UE_INLINE_GENERATED_CPP_BY_NAME(LyraTestEnemyQuery)

static bool IsAliveEnemyCharacter(ACharacter* Char, APawn* LocalPawn)
{
	if (!Char || !Char->GetController()) return false;
	if (Char->GetController()->IsPlayerController()) return false;
	if (LocalPawn && Char == LocalPawn) return false;
	if (ULyraHealthComponent* Health = ULyraHealthComponent::FindHealthComponent(Char))
	{
		if (Health->IsDeadOrDying()) return false;
	}
	return true;
}

static UWorld* GetWorldForAutomation(UObject* WorldContextObject)
{
	UWorld* World = nullptr;
	if (GEngine && WorldContextObject)
	{
		World = GEngine->GetWorldFromContextObject(WorldContextObject, EGetWorldErrorMode::ReturnNull);
	}
	if (World)
	{
		return World;
	}
	if (GEngine)
	{
		World = GEngine->GetCurrentPlayWorld();
		if (World)
		{
			return World;
		}
		for (const FWorldContext& Ctx : GEngine->GetWorldContexts())
		{
			if (Ctx.WorldType == EWorldType::Game || Ctx.WorldType == EWorldType::PIE)
			{
				if (UWorld* CtxWorld = Ctx.World())
				{
					return CtxWorld;
				}
			}
		}
	}
	return nullptr;
}

FString ULyraTestEnemyQuery::GetEnemyLocationsAsString(UObject* WorldContextObject, int32 PlayerIndex)
{
	UWorld* World = GetWorldForAutomation(WorldContextObject);
	if (!World)
	{
		return FString();
	}

	TArray<AActor*> Characters;
	UGameplayStatics::GetAllActorsOfClass(World, ACharacter::StaticClass(), Characters);

	UGameInstance* GI = World->GetGameInstance();
	APlayerController* LocalPC = GI ? GI->GetFirstLocalPlayerController() : nullptr;
	APawn* LocalPawn = LocalPC ? LocalPC->GetPawn() : nullptr;

	FString Result;
	for (AActor* Actor : Characters)
	{
		ACharacter* Char = Cast<ACharacter>(Actor);
		if (!IsAliveEnemyCharacter(Char, LocalPawn))
		{
			continue;
		}
		const FVector Loc = Char->GetActorLocation();
		if (Result.Len() > 0)
		{
			Result += TEXT("|");
		}
		Result += FString::Printf(TEXT("%.2f,%.2f,%.2f"), Loc.X, Loc.Y, Loc.Z);
	}
	return Result;
}

FString ULyraTestEnemyQuery::GetTestPositionsAsString(UObject* WorldContextObject, int32 PlayerIndex)
{
	UWorld* World = GetWorldForAutomation(WorldContextObject);
	if (!World)
	{
		return FString();
	}

	UGameInstance* GI = World->GetGameInstance();
	APlayerController* LocalPC = GI ? GI->GetFirstLocalPlayerController() : nullptr;
	APawn* LocalPawn = LocalPC ? LocalPC->GetPawn() : nullptr;

	FString Result;

	if (LocalPC)
	{
		FVector PlayerLoc;
		if (LocalPawn)
		{
			PlayerLoc = LocalPawn->GetActorLocation();
		}
		else if (APlayerCameraManager* PCM = LocalPC->PlayerCameraManager)
		{
			PlayerLoc = PCM->GetCameraLocation();
		}
		else
		{
			PlayerLoc = LocalPC->GetFocalLocation();
		}
		Result = FString::Printf(TEXT("P,%.2f,%.2f,%.2f"), PlayerLoc.X, PlayerLoc.Y, PlayerLoc.Z);
	}

	TArray<AActor*> Characters;
	UGameplayStatics::GetAllActorsOfClass(World, ACharacter::StaticClass(), Characters);
	for (AActor* Actor : Characters)
	{
		ACharacter* Char = Cast<ACharacter>(Actor);
		if (!IsAliveEnemyCharacter(Char, LocalPawn))
		{
			continue;
		}
		const FVector Loc = Char->GetActorLocation();
		if (Result.Len() > 0)
		{
			Result += TEXT("|");
		}
		Result += FString::Printf(TEXT("E,%.2f,%.2f,%.2f"), Loc.X, Loc.Y, Loc.Z);
	}
	return Result;
}

FString ULyraTestEnemyQuery::GetEnemyOnlyTestPositionsAsString(UObject* WorldContextObject, int32 PlayerIndex)
{
	UWorld* World = GetWorldForAutomation(WorldContextObject);
	if (!World)
	{
		return FString();
	}

	UGameInstance* GI = World->GetGameInstance();
	APlayerController* LocalPC = GI ? GI->GetFirstLocalPlayerController() : nullptr;
	APawn* LocalPawn = LocalPC ? LocalPC->GetPawn() : nullptr;
	UObject* LocalViewAgent = LocalPawn ? static_cast<UObject*>(LocalPawn) : static_cast<UObject*>(LocalPC);

	ULyraTeamSubsystem* TeamSub = World->GetSubsystem<ULyraTeamSubsystem>();

	FString Result;
	if (LocalPC)
	{
		FVector PlayerLoc;
		if (LocalPawn)
		{
			PlayerLoc = LocalPawn->GetActorLocation();
		}
		else if (APlayerCameraManager* PCM = LocalPC->PlayerCameraManager)
		{
			PlayerLoc = PCM->GetCameraLocation();
		}
		else
		{
			PlayerLoc = LocalPC->GetFocalLocation();
		}
		Result = FString::Printf(TEXT("P,%.2f,%.2f,%.2f"), PlayerLoc.X, PlayerLoc.Y, PlayerLoc.Z);
	}

	TArray<AActor*> Characters;
	UGameplayStatics::GetAllActorsOfClass(World, ACharacter::StaticClass(), Characters);
	for (AActor* Actor : Characters)
	{
		ACharacter* Char = Cast<ACharacter>(Actor);
		if (!IsAliveEnemyCharacter(Char, LocalPawn))
		{
			continue;
		}
		if (TeamSub && LocalViewAgent)
		{
			if (TeamSub->CompareTeams(LocalViewAgent, Char) != ELyraTeamComparison::DifferentTeams)
			{
				continue;
			}
		}
		const FVector Loc = Char->GetActorLocation();
		if (Result.Len() > 0)
		{
			Result += TEXT("|");
		}
		Result += FString::Printf(TEXT("E,%.2f,%.2f,%.2f"), Loc.X, Loc.Y, Loc.Z);
	}
	return Result;
}

static void ApplyLocalPlayerLookAt(UWorld* World, float TargetX, float TargetY, float TargetZ)
{
	UGameInstance* GI = World->GetGameInstance();
	if (!GI) return;
	APlayerController* PC = GI->GetFirstLocalPlayerController();
	if (!PC) return;

	FVector ViewLocation;
	FRotator ViewRotation;
	if (APlayerCameraManager* PCM = PC->PlayerCameraManager)
	{
		PCM->GetCameraViewPoint(ViewLocation, ViewRotation);
	}
	else
	{
		PC->GetPlayerViewPoint(ViewLocation, ViewRotation);
	}

	FVector Dir = (FVector(TargetX, TargetY, TargetZ) - ViewLocation).GetSafeNormal();
	if (Dir.IsNearlyZero()) return;

	float YawDeg = FMath::RadiansToDegrees(FMath::Atan2(Dir.Y, Dir.X));
	float PitchDeg = FMath::RadiansToDegrees(FMath::Asin(FMath::Clamp(Dir.Z, -1.f, 1.f)));
	PitchDeg = FMath::Clamp(PitchDeg, -89.f, 89.f);
	PC->SetControlRotation(FRotator(PitchDeg, YawDeg, 0.f));
}

FString ULyraTestEnemyQuery::GetEnemyOnlyPositionsAndAimAt(UObject* WorldContextObject, int32 PlayerIndex, float TargetX, float TargetY, float TargetZ, bool bFire)
{
	UWorld* World = GetWorldForAutomation(WorldContextObject);
	if (!World)
	{
		return FString();
	}

	UGameInstance* GI = World->GetGameInstance();
	if (ULyraTestSupportSubsystem* Sub = GI ? GI->GetSubsystem<ULyraTestSupportSubsystem>() : nullptr)
	{
		Sub->SetLocalPlayerLookAtWorldPosition(TargetX, TargetY, TargetZ);
		if (bFire)
		{
			Sub->SimulatePrimaryFire();
		}
	}
	else
	{
		ApplyLocalPlayerLookAt(World, TargetX, TargetY, TargetZ);
	}

	return GetEnemyOnlyTestPositionsAsString(WorldContextObject, PlayerIndex);
}

void ULyraTestEnemyQuery::SetLocalPlayerInvincible(UObject* WorldContextObject, bool bEnable)
{
	UWorld* World = GetWorldForAutomation(WorldContextObject);
	if (!World) return;
	UGameInstance* GI = World->GetGameInstance();
	if (!GI) return;
	if (ULyraTestSupportSubsystem* Sub = GI->GetSubsystem<ULyraTestSupportSubsystem>())
	{
		Sub->SetLocalPlayerInvincible(bEnable);
	}
}

void ULyraTestEnemyQuery::SetLocalPlayerInfiniteAmmo(UObject* WorldContextObject, bool bEnable)
{
	UWorld* World = GetWorldForAutomation(WorldContextObject);
	if (!World) return;
	UGameInstance* GI = World->GetGameInstance();
	if (!GI) return;
	if (ULyraTestSupportSubsystem* Sub = GI->GetSubsystem<ULyraTestSupportSubsystem>())
	{
		Sub->SetLocalPlayerInfiniteAmmo(bEnable);
	}
}

int64 ULyraTestEnemyQuery::GetLocalPlayerPawnId(UObject* WorldContextObject, int32 PlayerIndex)
{
	UWorld* World = GetWorldForAutomation(WorldContextObject);
	if (!World) return 0;
	UGameInstance* GI = World->GetGameInstance();
	APlayerController* LocalPC = GI ? GI->GetFirstLocalPlayerController() : nullptr;
	APawn* LocalPawn = LocalPC ? LocalPC->GetPawn() : nullptr;
	if (!LocalPawn) return 0;
	return static_cast<int64>(reinterpret_cast<uintptr_t>(LocalPawn));
}
