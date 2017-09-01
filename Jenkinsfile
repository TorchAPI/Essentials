def test_with_torch(branch)
{
	try {
		stage('Acquire Torch ' + branch) {
			bat 'IF EXIST TorchBinaries RMDIR /S /Q TorchBinaries'
			bat 'mkdir TorchBinaries'
			step([$class: 'CopyArtifact', projectName: "Torch/Torch/${branch}", filter: "**/Torch*.dll", flatten: true, fingerprintArtifacts: true, target: "TorchBinaries"])
			step([$class: 'CopyArtifact', projectName: "Torch/Torch/${branch}", filter: "**/Torch*.exe", flatten: true, fingerprintArtifacts: true, target: "TorchBinaries"])
		}

		stage('Build + Torch ' + branch) {
			currentBuild.description = bat(returnStdout: true, script: '@powershell -File Versioning/version.ps1').trim()
			bat "\"${tool 'MSBuild'}msbuild\" Essentials.sln /p:Configuration=Release /p:Platform=x64 /t:Clean"
			bat "\"${tool 'MSBuild'}msbuild\" Essentials.sln /p:Configuration=Release /p:Platform=x64"
		}

	
		stage('Test + Torch ' + branch) {
			bat 'IF NOT EXIST reports MKDIR reports'
			bat "\"packages/xunit.runner.console.2.2.0/tools/xunit.console.exe\" \"bin-test/x64/Release/Essentials.Tests.dll\" -parallel none -xml \"reports/Essentials.Tests.xml\""
		    step([
		        $class: 'XUnitBuilder',
		        thresholdMode: 1,
		        thresholds: [[$class: 'FailedThreshold', failureThreshold: '1']],
		        tools: [[
		            $class: 'XUnitDotNetTestType',
		            deleteOutputFiles: true,
		            failIfNotNew: true,
		            pattern: 'reports/*.xml',
		            skipNoTestFiles: false,
		            stopProcessingIfError: true
		        ]]
		    ])
		}

		return true
	} catch (e) {
		return false
	}
}

node {
	stage('Checkout') {
		checkout scm
		bat 'git pull --tags'
	}

	stage('Acquire SE') {
		bat 'powershell -File Jenkins/jenkins-grab-se.ps1'
		bat 'IF EXIST GameBinaries RMDIR GameBinaries'
		bat 'mklink /J GameBinaries "C:/Steam/Data/DedicatedServer64/"'
	}

	stage('Acquire NuGet Packages') {
		bat 'nuget restore Essentials.sln'
	}

	if (env.BRANCH_NAME == "master") {
		result = test_with_torch("master")
	} else {
		result = test_with_torch("staging")
	}
	if (result) {
		currentBuild.result = "SUCCESS"
		stage('Archive') {
			archiveArtifacts artifacts: "bin/x64/Release/Essentials.dll", caseSensitive: false, fingerprint: true, onlyIfSuccessful: true
		}
	}
	else
		currentBuild.result = "FAIL"
}