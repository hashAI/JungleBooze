# Ruby tools for iOS signing and TestFlight upload. Install with: bundle install
# (tools/build/build_ios.sh does this for you). Needs Ruby 3.2 or newer; macOS's built-in Ruby is too old.
source "https://rubygems.org"

gem "fastlane", "~> 2.240"

plugins_path = File.join(File.dirname(__FILE__), "fastlane", "Pluginfile")
eval_gemfile(plugins_path) if File.exist?(plugins_path)
